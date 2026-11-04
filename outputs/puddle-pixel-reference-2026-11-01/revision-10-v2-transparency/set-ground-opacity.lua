-- Edit revision2 native sources: only the ground-layer opacity changes.
local src=assert(app.params.sourceDir)
local out=assert(app.params.outputDir)
local pc=app.pixelColor
local opacity=166 -- 65.1%; all other layers retain their original opacity.
local function rgb(r,g,b) return pc.rgba(r,g,b,255) end
local function floorImage(w,h,right,ox,oy)
  local a=Image(w,h,ColorMode.RGB)
  for y=0,h-1 do for x=0,w-1 do
    local seam=((x+(ox or 0))%16==0 or (y+(oy or 0))%16==0)
    a:drawPixel(x,y,right and (seam and rgb(56,64,71) or rgb(65,74,80)) or (seam and rgb(36,43,48) or rgb(43,51,57)))
  end end
  return a
end
local function exactPalette(s)
  local seen,colors={},{}
  for f=1,#s.frames do
    local a=Image(s.width,s.height,ColorMode.RGB);a:drawSprite(s,f)
    for p in a:pixels() do local c=p();if not seen[c] then seen[c]=true;colors[#colors+1]=c end end
  end
  assert(#colors<256)
  local p=Palette(#colors);for i,c in ipairs(colors) do p:setColor(i-1,c) end;s:setPalette(p)
end
local report={};local made={}
for k,name in ipairs({'alcohol','fire'}) do
  local s=app.open(src..'/'..name..'.aseprite')
  assert(s.width==64 and s.height==64 and #s.frames==4 and #s.layers==3)
  assert(s.layers[1].name=='01 Ground - fixed flat footprint' and s.layers[1].opacity==255)
  s.layers[1].opacity=opacity
  s:saveAs(out..'/'..name..'.aseprite')
  local saved=app.open(out..'/'..name..'.aseprite')
  local original=app.open(src..'/'..name..'.aseprite')
  assert(saved.layers[1].opacity==opacity)
  for i=1,3 do
    assert(saved.layers[i].name==original.layers[i].name)
    if i>1 then assert(saved.layers[i].opacity==original.layers[i].opacity) end
    for f=1,4 do
      local a,b=saved.layers[i]:cel(f),original.layers[i]:cel(f)
      assert(a.image:isEqual(b.image) and a.position.x==b.position.x and a.position.y==b.position.y and a.opacity==b.opacity)
      assert(saved.frames[f].duration==original.frames[f].duration)
    end
  end
  assert(saved.tags[1].name==original.tags[1].name and saved.tags[1].fromFrame.frameNumber==1 and saved.tags[1].toFrame.frameNumber==4)
  assert(saved.slices[1].pivot.x==32 and saved.slices[1].pivot.y==32)
  local sheet=Image(256,64,ColorMode.RGB)
  local review=Sprite(64,64,ColorMode.RGB)
  for f=1,4 do
    local a=Image(64,64,ColorMode.RGB);a:drawSprite(saved,f)
    a:saveAs(out..'/'..name..'-frame-'..f..'.png')
    sheet:drawImage(a,Point((f-1)*64,0))
    if f>1 then review:newEmptyFrame() end;review.frames[f].duration=saved.frames[f].duration
    local b=floorImage(64,64,k==2,k==1 and 6 or 82,8);b:drawImage(a)
    local old=review.layers[1]:cel(f);if old then review:deleteCel(old) end
    review:newCel(review.layers[1],f,b,Point(0,0))
  end
  sheet:saveAs(out..'/'..name..'-sheet.png')
  exactPalette(review);review:saveAs(out..'/'..name..'.gif');review:close()
  report[#report+1]=name..': ground opacity166/255; all12 cel pixel images/positions/opacities unchanged; upper layer opacity, timings, tag and pivot retained.'
  original:close();saved:close();made[k]=s
end
-- Rebuild exactly the same review layout, background and160x20ms schedule.
local preview=Sprite(152,80,ColorMode.RGB)
preview.layers[1].name='Review only - original revision2 background'
for f=1,160 do
  if f>1 then preview:newEmptyFrame() end;preview.frames[f].duration=.02
  local a=floorImage(152,80,false,0,0);local right=floorImage(76,80,true,76,0);a:drawImage(right,Point(76,0))
  local alcohol,fire=Image(64,64,ColorMode.RGB),Image(64,64,ColorMode.RGB)
  alcohol:drawSprite(made[1],math.floor((f-1)/8)%4+1)
  fire:drawSprite(made[2],math.floor((f-1)/5)%4+1)
  a:drawImage(alcohol,Point(6,8));a:drawImage(fire,Point(82,8))
  local old=preview.layers[1]:cel(f);if old then preview:deleteCel(old) end
  preview:newCel(preview.layers[1],f,a,Point(0,0))
end
preview:resize(608,320);exactPalette(preview)
preview:saveAs(out..'/comparison-preview.aseprite');preview:saveCopyAs(out..'/comparison-preview.gif')
Image(preview):saveAs(out..'/comparison-preview.png');preview:close()
for _,s in ipairs(made) do s:close() end
local log=assert(io.open(out..'/verification.txt','w'));log:write(table.concat(report,'\n')..'\nAseprite '..tostring(app.version)..'\n');log:close()
