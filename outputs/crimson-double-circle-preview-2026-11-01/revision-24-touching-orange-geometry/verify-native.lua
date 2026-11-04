local out=assert(app.params.outputDir)
for _,name in ipairs({'inner','outer','combined-192'}) do
 local s=assert(app.open(out..'/'..name..'.png'))
 assert(s.width==192 and s.height==192);s:close()
end
local s=assert(app.open(out..'/opposing-rotation.gif'))
assert(s.width==480 and s.height==480 and #s.frames==120)
for _,frame in ipairs(s.frames) do assert(math.abs(frame.duration-.08)<.0001) end
s:close()
local f=assert(io.open(out..'/media-verification.json','w'))
f:write('{"pngDimensions":[192,192],"gifDimensions":[480,480],"frames":120,"durationMs":80,"loopMs":9600,"verifiedBy":"Aseprite native reopen"}\n');f:close()
print('Native reopen passed: three 192px PNGs; 480px GIF, 120 frames, 80ms each.')
