#Requires AutoHotkey v2.0
#SingleInstance Force

; Reliable hotkeys:
; Ctrl+Alt+Numpad1 조사, Ctrl+Alt+Numpad2 계획, Ctrl+Alt+Numpad3 구현
; Ctrl+Alt+Numpad4 검증, Ctrl+Alt+Numpad5 실험, Ctrl+Alt+Numpad6 작은 수정
; Korean hotstrings are also available, but can depend on IME behavior.
PasteTemplate(fileName) {
    templatePath := A_ScriptDir "\..\Docs\_templates\" fileName
    if !FileExist(templatePath) {
        MsgBox "Task Brief template not found:`n" templatePath, "Codex Task Brief"
        return
    }

    oldClip := ClipboardAll()
    try {
        A_Clipboard := FileRead(templatePath, "UTF-8")
        if !ClipWait(1) {
            MsgBox "Clipboard update failed.", "Codex Task Brief"
            return
        }
        SendInput "^v"
        Sleep 400
    } finally {
        A_Clipboard := oldClip
    }
}

^!Numpad1::PasteTemplate("TaskBrief-Investigation.txt")
^!Numpad2::PasteTemplate("TaskBrief-Planning.txt")
^!Numpad3::PasteTemplate("TaskBrief-Implementation.txt")
^!Numpad4::PasteTemplate("TaskBrief-Verification.txt")
^!Numpad5::PasteTemplate("TaskBrief-Spike.txt")
^!Numpad6::PasteTemplate("TaskBrief-MicroFix.txt")

:O:;조사::
{
    PasteTemplate("TaskBrief-Investigation.txt")
}

:O:;계획::
{
    PasteTemplate("TaskBrief-Planning.txt")
}

:O:;구현::
{
    PasteTemplate("TaskBrief-Implementation.txt")
}

:O:;검증::
{
    PasteTemplate("TaskBrief-Verification.txt")
}

:O:;실험::
{
    PasteTemplate("TaskBrief-Spike.txt")
}

:O:;작수::
{
    PasteTemplate("TaskBrief-MicroFix.txt")
}

:O:;cxi::
{
    PasteTemplate("TaskBrief-Investigation.txt")
}

:O:;cxp::
{
    PasteTemplate("TaskBrief-Planning.txt")
}

:O:;cximp::
{
    PasteTemplate("TaskBrief-Implementation.txt")
}

:O:;cxv::
{
    PasteTemplate("TaskBrief-Verification.txt")
}

:O:;cxs::
{
    PasteTemplate("TaskBrief-Spike.txt")
}

:O:;cxm::
{
    PasteTemplate("TaskBrief-MicroFix.txt")
}
