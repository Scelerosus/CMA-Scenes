param(
    [string]$GameDir = 'C:\Games\Corrupted Modeling Agency 0.66',
    [string[]]$Plugins = @(
        (Join-Path $PSScriptRoot '../../CMAClothingFix/bin/Release/CMAClothingFix.dll'),
        (Join-Path $PSScriptRoot '../bin/Release/CMAScenes.dll')
    )
)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $GameDir 'BepInEx/core/Mono.Cecil.dll')
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory((Join-Path $GameDir 'BepInEx/interop'))
$resolver.AddSearchDirectory((Join-Path $GameDir 'BepInEx/core'))
$parameters = [Mono.Cecil.ReaderParameters]::new()
$parameters.AssemblyResolver = $resolver
function AllTypes($types) {
    foreach ($type in $types) { $type; AllTypes $type.NestedTypes }
}
function StrippedPath([Mono.Cecil.MethodReference]$reference, $visited, [int]$depth = 0) {
    if ($depth -gt 20 -or !$reference.DeclaringType.FullName.StartsWith('UnityEngine.') -or !$visited.Add($reference.FullName)) { return }
    $method = $reference.Resolve()
    if (!$method.HasBody) { return }
    if ($method.Body.Instructions | Where-Object { $_.Operand -eq 'Method unstripping failed' }) { return $method.FullName }
    foreach ($instruction in $method.Body.Instructions) {
        if ($instruction.Operand -is [Mono.Cecil.MethodReference]) {
            $path = StrippedPath $instruction.Operand $visited ($depth + 1)
            if ($path) { return $method.FullName + ' -> ' + $path }
        }
    }
}
# Confirm the scanner detects the exact stripped call that broke Outfit in 0.3.40.
$imgui = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'BepInEx/interop/UnityEngine.IMGUIModule.dll'), $parameters)
$content = $imgui.MainModule.Types | Where-Object FullName -eq 'UnityEngine.GUIContent'
$bad = $content.Methods | Where-Object { $_.Name -eq 'Temp' -and $_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.Name -eq 'Il2CppStringArray' }
if (!(StrippedPath $bad ([System.Collections.Generic.HashSet[string]]::new()))) { throw 'Stripped API scanner self-check failed' }
$checked = [System.Collections.Generic.HashSet[string]]::new()
$failures = [System.Collections.Generic.List[string]]::new()
foreach ($plugin in $Plugins) {
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($plugin, $parameters)
    foreach ($type in (AllTypes $assembly.MainModule.Types)) {
        foreach ($method in $type.Methods) {
            if (!$method.HasBody) { continue }
            foreach ($instruction in $method.Body.Instructions) {
                $called = $instruction.Operand
                if ($called -isnot [Mono.Cecil.MethodReference] -or !$called.DeclaringType.FullName.StartsWith('UnityEngine.') -or !$checked.Add($called.FullName)) { continue }
                $path = StrippedPath $called ([System.Collections.Generic.HashSet[string]]::new())
                if ($path) {
                    # Optional system font creation already catches this and falls back to the game's font.
                    if ($called.DeclaringType.FullName -eq 'UnityEngine.Font' -and $called.Name -eq 'CreateDynamicFontFromOSFont') { continue }
                    $failures.Add($method.FullName + ': ' + $path)
                }
            }
        }
    }
    $assembly.Dispose()
}
$imgui.Dispose()
$resolver.Dispose()
if ($failures.Count) { $failures | Write-Output; throw "$($failures.Count) required Unity API calls reach stripped methods" }
Write-Output "PASS: $($checked.Count) Unity API entry points checked against the installed game's interop assemblies; stripped API self-check passed. Native runtime behavior still needs game verification."
