-- Exact, unscaled side-by-side comparison; no modifications to either source image.
local out=app.params.outputDir
local project=app.params.projectDir
local function crop(im,x,y,w,h)
 local r=Image(w,h,ColorMode.RGB);r:drawImage(im,Point(-x,-y));return r
end
local checks={}
for _,kind in ipairs({'monster','boss'}) do
 local beforePath=project..'/outputs/encyclopedia-tab-visibility-reveal-2026-10-12/current-actual-'..kind..'-1920x1080.png'
 local before=crop(Image{fromFile=beforePath},400,220,1180,740)
 local after=Image{fromFile=out..'/../'..kind..'-preview.png'}
 assert(after.width==1180 and after.height==740)
 local s=Sprite(2432,850,ColorMode.RGB)
 s.layers[1].name='Comparison background'
 s.cels[1].image:clear(app.pixelColor.rgba(30,35,44,255))
 local left=s:newLayer();left.name='Existing Unity capture - exact 1:1 crop'
 s:newCel(left,1,before,Point(24,72))
 local right=s:newLayer();right.name='Proposed Aseprite preview - exact 1:1 image'
 s:newCel(right,1,after,Point(1228,72))
 local text=s:newLayer();text.name='Comparison labels'
 s:newCel(text,1,Image{fromFile=out..'/'..kind..'-labels.png'})
 s:saveAs(out..'/'..kind..'-before-after.aseprite')
 s:saveCopyAs(out..'/'..kind..'-before-after.png')
 s:close()
 local merged=Image{fromFile=out..'/'..kind..'-before-after.png'}
 for y=0,739 do for x=0,1179 do
  assert(merged:getPixel(24+x,72+y)==before:getPixel(x,y),'Before pixels changed')
  assert(merged:getPixel(1228+x,72+y)==after:getPixel(x,y),'After pixels changed')
 end end
 checks[#checks+1]='PASS '..kind..': both 1180x740 panels pixel-identical to source/crop; no resizing.'
end
local report=io.open(out..'/validation.txt','w');report:write(table.concat(checks,'\n')..'\n');report:close()
print('ENCYCLOPEDIA_COMPARISON_PASS')
