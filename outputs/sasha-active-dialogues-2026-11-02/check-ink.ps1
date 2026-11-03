$ErrorActionPreference = 'Stop'
Add-Type -Path 'C:/Users/user/Desktop/CapstoneProject/Temp/bin/Debug/Ink-Libraries.dll'
$inkJson = Get-Content -LiteralPath 'Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.json' -Raw -Encoding UTF8
$lines = [Collections.Generic.List[string]]::new()
foreach ($path in 'affection_0_a','affection_0_b','affection_0_c','affection_1_c') {
    foreach ($choice in 0,1) {
        $story = [Ink.Runtime.Story]::new($inkJson)
        $story.ChoosePathString($path, $true, [object[]]@())
        $openingFace = $null
        while ($story.canContinue) {
            $null = $story.Continue()
            foreach ($tag in $story.currentTags) { if ($tag.StartsWith('face:')) { $openingFace = $tag } }
        }
        if ($openingFace -ne 'face: 1001: Normal' -or $story.currentChoices.Count -ne 2) { throw "Opening/choices: $path" }
        $story.ChooseChoiceIndex($choice)
        $tags = [Collections.Generic.List[string]]::new()
        while ($story.canContinue) { $null = $story.Continue(); foreach ($tag in $story.currentTags) { $tags.Add($tag) } }
        if ($choice -eq 0) {
            if (-not $tags.Contains('add_aff: 1') -or -not $tags.Contains('face: 1001: Happy') -or $tags.Contains('choice_fail')) { throw "Positive response: $path" }
        } else {
            if (-not $tags.Contains('choice_fail') -or -not $tags.Contains('face: 1001: Surprised') -or ($tags | Where-Object { $_.StartsWith('add_aff:') })) { throw "No-gain response: $path" }
        }
        $lines.Add("PASS $path choice ${choice}: Normal opening, two choices, expected face and affection tags")
    }
}
$lines.Add('PASS: 8 active dialogue branch checks using the production Ink runtime.')
[IO.File]::WriteAllLines('C:/Users/user/Desktop/CapstoneProject/outputs/sasha-active-dialogues-2026-11-02/ink-checks.txt', $lines, [Text.UTF8Encoding]::new($false))
Write-Output $lines[$lines.Count - 1]
