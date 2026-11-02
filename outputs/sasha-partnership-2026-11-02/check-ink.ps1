$ErrorActionPreference = 'Stop'
Add-Type -Path 'C:/Users/user/Desktop/CapstoneProject/Temp/bin/Debug/Ink-Libraries.dll'
$taskRoot = 'C:/Users/user/Desktop/CapstoneProject/outputs/sasha-partnership-2026-11-02'
$inkPath = Join-Path $taskRoot 'staging/Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink'
$sourceText = Get-Content -LiteralPath $inkPath -Raw -Encoding UTF8
$compiledStory = [Ink.Compiler]::new($sourceText, $null).Compile()
if ($null -eq $compiledStory) { throw 'Ink compile failed' }
$jsonText = $compiledStory.ToJson()
[IO.File]::WriteAllText([IO.Path]::ChangeExtension($inkPath, '.json'), $jsonText, [Text.UTF8Encoding]::new($false))
$rows = Get-Content -LiteralPath (Join-Path $taskRoot 'rows.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$checks = [Collections.Generic.List[string]]::new()
function Check($condition, $label) {
    if (-not $condition) { throw $label }
    $checks.Add('PASS ' + $label)
}
Check ($null -ne $compiledStory) 'Existing production Ink compiler compiles the whole merchant source'
foreach ($choiceIndex in 0,1) {
    $story = [Ink.Runtime.Story]::new($jsonText)
    $story.ChoosePathString('affection_0_c', $true, [object[]]@())
    Check ($story.Continue().Trim() -ceq $rows.'dialogue.merchant.run.affection_0_c.0'[0]) 'First opening matches approved partnership greeting'
    Check ($story.currentTags.Contains('face: 1001: Normal')) 'Opening resets Normal'
    Check ($story.currentChoices.Count -eq 0 -and $story.canContinue) 'Choices wait for the second speech window'
    Check ($story.Continue().Trim() -ceq $rows.'dialogue.merchant.run.affection_0_c.5'[0]) 'Second opening matches approved share joke'
    Check (-not $story.canContinue -and $story.currentChoices.Count -eq 2) 'Exactly two openings before two choices'
    Check ($story.currentChoices[0].text.Trim() -ceq $rows.'dialogue.merchant.run.affection_0_c.1'[0]) 'Positive choice wording matches'
    Check ($story.currentChoices[1].text.Trim() -ceq $rows.'dialogue.merchant.run.affection_0_c.3'[0]) 'No-gain choice wording matches'
    $story.ChooseChoiceIndex($choiceIndex)
    $lines = [Collections.Generic.List[string]]::new()
    $tags = [Collections.Generic.List[string]]::new()
    while ($story.canContinue) {
        $line = $story.Continue().Trim()
        if ($line.Length -gt 0) { $lines.Add($line) }
        foreach ($tag in $story.currentTags) { $tags.Add($tag) }
    }
    $reply = if ($choiceIndex -eq 0) { $rows.'dialogue.merchant.run.affection_0_c.2'[0] } else { $rows.'dialogue.merchant.run.affection_0_c.4'[0] }
    Check ($lines.Count -eq 1 -and $lines[0] -ceq $reply) 'Branch has exactly one approved response window'
    if ($choiceIndex -eq 0) {
        Check ($tags.Contains('face: 1001: Happy') -and $tags.Contains('add_aff: 1') -and -not $tags.Contains('choice_fail')) 'Positive branch uses Happy and existing +1 request'
    } else {
        Check ($tags.Contains('face: 1001: Surprised') -and $tags.Contains('choice_fail') -and -not ($tags | Where-Object { $_.StartsWith('add_aff:') })) 'No-gain branch uses Surprised and grants no affection'
    }
    $story.ChoosePathString('affection_0_c', $true, [object[]]@())
    $null = $story.Continue()
    Check ($story.currentTags.Contains('face: 1001: Normal')) 'Next conversation resets Normal after either branch'
}
$checks.Add('PASS: ' + $checks.Count + ' focused Ink checks; no Unity Editor launch.')
[IO.File]::WriteAllLines((Join-Path $taskRoot 'ink-checks.txt'), $checks, [Text.UTF8Encoding]::new($false))
Write-Output $checks[$checks.Count - 1]
