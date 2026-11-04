local out=assert(app.params.outputDir)
local s=app.open(assert(app.params.bubblePath))
local rows={'size='..s.width..'x'..s.height,'frames='..#s.frames,'layers='..#s.layers,'mode='..tostring(s.colorMode)}
local sheet=Image(s.width*#s.frames,s.height,ColorMode.RGB)
for f,frame in ipairs(s.frames) do
  local a=Image(s.width,s.height,ColorMode.RGB);a:drawSprite(s,f)
  a:saveAs(out..'/existing-bubble-frame-'..f..'.png');sheet:drawImage(a,Point((f-1)*s.width,0))
  rows[#rows+1]='frame '..f..' duration='..frame.duration
end
for _,l in ipairs(s.layers) do rows[#rows+1]='layer '..l.name..' opacity='..l.opacity end
for _,t in ipairs(s.tags) do rows[#rows+1]='tag '..t.name..' '..t.fromFrame.frameNumber..' '..t.toFrame.frameNumber end
sheet:saveAs(out..'/existing-bubble-sheet.png')
local review=Sprite(sheet.width,sheet.height,ColorMode.RGB)
local bg=Image(sheet.width,sheet.height,ColorMode.RGB)
for p in bg:pixels() do p(app.pixelColor.rgba(43,51,57,255)) end
bg:drawImage(sheet,Point(0,0));review:newCel(review.layers[1],1,bg)
review:resize(sheet.width*6,sheet.height*6);Image(review):saveAs(out..'/existing-bubble-review.png')
local file=assert(io.open(out..'/existing-bubble-inspection.txt','w'));file:write(table.concat(rows,'\n'));file:close()
review:close();s:close()
