// MSBossNPC encounter dialogue sample.
// Assign the compiled JSON to NPCData > Boss Encounter Ink.
// Each Boss Encounter Dialogue rule should set Start Path to one of these knots.

=== first_encounter ===
# speaker: 1003
# face: 1003: Normal
# anim: slow
나를 찾아온 건 네가 처음이 아니야. [pause=0.25]그리고 마지막도 아닐 거고. # loc:dialogue.shadowbossrepeatencounterdialogue_animated.048fdb20c2

# face: 1003: Normal
# anim: normal
그래도 여기까지 온 건 기억해둘게. # loc:dialogue.shadowbossrepeatencounterdialogue_animated.5d1f3f21ed
-> END

=== second_encounter ===
# speaker: 1003
# face: 1003: Panic
# anim: angry
[shake]????[/shake] 너 어떻게 살아있냐?! # loc:dialogue.shadowbossrepeatencounterdialogue_animated.3d07c12402

# face: 1003: Normal
# anim: normal
아니, 좋아. 이번엔 조금 더 똑바로 봐줄게. # loc:dialogue.shadowbossrepeatencounterdialogue_animated.9ac8745e1c
-> END

=== low_affection ===
# speaker: 1003
# face: 1003: Panic
# anim: angry
이제는 [punch]그만 좀 찾아와[/punch]! # loc:dialogue.shadowbossrepeatencounterdialogue_animated.3052c800bf

# face: 1003: Normal
# anim: angry
매번 이렇게 나타나면 나도 반응하기 힘들다고. # loc:dialogue.shadowbossrepeatencounterdialogue_animated.effdc00fdf
-> END

=== normal_affection ===
# speaker: 1003
# face: 1003: Normal
# anim: normal
이제 슬슬 익숙해지네. # loc:dialogue.shadowbossrepeatencounterdialogue_animated.e98e59d17f

# face: 1003: Normal
# anim: slow
네가 오는 게 이상하지 않게 느껴지는 것도 좀 이상하지만. # loc:dialogue.shadowbossrepeatencounterdialogue_animated.9176b54eec
-> END

=== high_affection ===
# speaker: 1003
# face: 1003: Smile
# anim: normal
왔구나. 생각보다 빨랐네. # loc:dialogue.shadowbossrepeatencounterdialogue_animated.a63821af14

# face: 1003: Normal
# anim: slow
이번엔 네 이야기를 조금 더 들어줄 수 있을지도 몰라. # loc:dialogue.shadowbossrepeatencounterdialogue_animated.44ee5beea2
-> END

=== much_time_left ===
# speaker: 1003
# face: 1003: Surprise
# anim: angry
[punch]오오오!?[/punch] 너 꽤 빨리 왔다!? # loc:dialogue.shadowbossrepeatencounterdialogue_animated.759b3cfc1d

# face: 1003: Smile
# anim: normal
그 정도 여유가 있으면 허세 좀 부려도 되겠는데. # loc:dialogue.shadowbossrepeatencounterdialogue_animated.82229470ea
-> END

=== low_time_left ===
# speaker: 1003
# face: 1003: Normal
# anim: slow
너 곧 쓰러질 것 같은 얼굴인데? # loc:dialogue.shadowbossrepeatencounterdialogue_animated.f46ecbc136

# face: 1003: Normal
# anim: normal
그래도 여기까지 왔다는 건 인정해줄게. # loc:dialogue.shadowbossrepeatencounterdialogue_animated.36d4197eac
-> END

=== low_health ===
# speaker: 1003
# face: 1003: Surprise
# anim: angry
잠깐, 너 [tremble]그 몸으로[/tremble] 싸우려고? # loc:dialogue.shadowbossrepeatencounterdialogue_animated.dd5f9a318c

# face: 1003: Normal
# anim: slow
무모한 건지 끈질긴 건지 아직은 모르겠네. # loc:dialogue.shadowbossrepeatencounterdialogue_animated.2f3389cc25
-> END

=== fallback ===
# speaker: 1003
# face: 1003: Normal
# anim: normal
또 왔네. # loc:dialogue.shadowbossrepeatencounterdialogue_animated.7531aabb90

# face: 1003: Normal
# anim: normal
좋아. 이번에도 네가 어디까지 버티는지 보자. # loc:dialogue.shadowbossrepeatencounterdialogue_animated.dba45168b1
-> END
