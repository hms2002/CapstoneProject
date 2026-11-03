local out='outputs/title-language-icon-revision-2026-11-01'
local names={'Normal','Hover','Pressed'}
local pc=app.pixelColor
local function load(path)
  local s=assert(app.open(path)); local im=Image(s.cels[1].image);s:close();return im
end
local before=load(out..'/before/TitleLanguageNormal.png')
local images={}
local reference={}
local report={}
local function white(p) return pc.rgbaR(p)==238 and pc.rgbaG(p)==226 and pc.rgbaB(p)==255 end
for n,state in ipairs(names) do
  local im=load(out..'/candidate/TitleLanguage'..state..'.png');images[n]=im
  local old=load(out..'/before/TitleLanguage'..state..'.png')
  assert(im.width==48 and im.height==48)
  local left,top,right,bottom=48,48,-1,-1
  local count,sumx,sumy=0,0,0
  for y=0,47 do for x=0,47 do
    local p=im:getPixel(x,y)
    if x<3 or y<3 or x>=45 or y>=45 then assert(p==old:getPixel(x,y),'Outer frame changed') end
    assert(pc.rgbaA(p)==255,'Nonopaque button pixel')
    local filled=white(p)
    if n==1 then reference[y*48+x]=filled else assert(reference[y*48+x]==filled,'State glyph drift') end
    if filled then
      left=math.min(left,x);top=math.min(top,y);right=math.max(right,x);bottom=math.max(bottom,y)
      count=count+1;sumx=sumx+x;sumy=sumy+y
    end
  end end
  local cx,cy=(left+right)/2,(top+bottom)/2
  assert(math.abs(cx-23.5)<=.5 and math.abs(cy-23.5)<=.5,'Glyph bounds off-center')
  report[#report+1]=state..': bounds '..left..','..top..' to '..right..','..bottom..'; center '..cx..','..cy..'; ink centroid '..string.format('%.2f,%.2f',sumx/count,sumy/count)..'; '..count..' pale pixels; outer frame unchanged.'
end
local compare=Sprite(104,48,ColorMode.RGB)
local l=compare.layers[1];compare:newCel(l,1,before,Point(0,0));l=compare:newLayer();compare:newCel(l,1,images[1],Point(56,0))
compare:saveCopyAs(out..'/before-after.png');compare:close()
local states=Sprite(160,48,ColorMode.RGB)
for i,im in ipairs(images) do local l=i==1 and states.layers[1] or states:newLayer();states:newCel(l,1,im,Point((i-1)*56,0)) end
states:saveCopyAs(out..'/states.png');states:close()
local file=assert(io.open(out..'/pixel-validation.txt','w'));file:write(table.concat(report,'\n')..'\nPASS: 48px dimensions, opaque pixels, geometric centering, shared glyph mask, unchanged outer frame.\n');file:close()
