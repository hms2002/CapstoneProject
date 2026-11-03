-- Aseprite revision: retain selected A buttons, remove header panel,
-- thicken the existing Galmuri9 raster and author a separate dark outline.
local root,out=app.params.root,app.params.output
local parent=out..'/..'
local pc=app.pixelColor
local function color(h)
  return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255)
end
local function resize(im,w,h)
  local dst=Image(w,h)
  for p in dst:pixels() do p(im:getPixel(math.floor(p.x*im.width/w),math.floor(p.y*im.height/h))) end
  return dst
end
local function add(sp,name,im,pos)
  local layer=sp:newLayer();layer.name=name
  sp:newCel(layer,1,im,pos or Point(0,0));return layer
end
local src=Image{fromFile=parent..'/typography/title.png'}
local pad=8
local w,h=src.width+pad*2,src.height+pad*2
local fill,outline=Image(w,h),Image(w,h)
-- One pixel expansion in every direction: the four-pixel main strokes
-- become six-pixel strokes, preserving the original letterforms and size.
for p in src:pixels() do
  if pc.rgbaA(p())>0 then
    for dy=-1,1 do for dx=-1,1 do
      fill:drawPixel(p.x+pad+dx,p.y+pad+dy,color('FFFFFF'))
    end end
  end
end
-- Four-pixel crisp outline around the bold mask. No rectangular backing.
for p in fill:pixels() do
  if pc.rgbaA(p())>0 then
    for dy=-4,4 do for dx=-4,4 do
      outline:drawPixel(p.x+dx,p.y+dy,color('17131C'))
    end end
  end
end
local title=Sprite(w,h,ColorMode.RGB)
title.layers[1].name='Outline - 4px dark charcoal'
title:newCel(title.layers[1],1,outline,Point(0,0))
add(title,'Bold face - white Galmuri9',fill)
title:saveAs(out..'/Level-UP-bold-outline.aseprite')
local titleFlat=Image(title);titleFlat:saveAs(out..'/Level-UP-bold-outline.png')
title:close()

local screen=app.open(parent..'/A-gilt-screen.aseprite')
local raw=Image{fromFile=root..'/outputs/localization-screen-review-2026-10-04/ko-hub.png'}
local bg=resize(raw,1920,1080)
for p in bg:pixels() do
  local v=p();p(pc.rgba(math.floor(pc.rgbaR(v)*56/255),math.floor(pc.rgbaG(v)*56/255),math.floor(pc.rgbaB(v)*56/255),pc.rgbaA(v)))
end
-- The original reference layer contained the baked black panel and title.
-- Rebuild only that layer from the same read-only background reference.
screen.layers[1]:cel(1).image=bg
screen.layers[1].name='Game background - no header panel'
local pos=Point(math.floor((1920-w)/2),math.floor(90-h/2))
add(screen,'Title outline - panel removed',outline,pos)
add(screen,'Title bold white face',fill,pos)
screen:saveAs(out..'/A-selected-screen.aseprite')
Image(screen):saveAs(out..'/A-selected-screen.png')

-- A clean 2x detail crop makes the new weight and outline easy to inspect.
local full=Image(screen)
local detail=Image(800,150);detail:drawImage(full,Point(-560,-15))
resize(detail,1600,300):saveAs(out..'/title-detail-2x.png')
screen:close()
local f=io.open(out..'/title-spec.json','w')
f:write(json.encode({
  selectedButton='A-gilt',text='Level UP',sourceFont='Galmuri9',
  sourceFontSize=48,boldExpansionPixels=1,outlinePixels=4,
  faceColor='#FFFFFF',outlineColor='#17131C',panel=false,
  titleCenter={x=960,y=90},width=w,height=h,
  note='Aseprite raster mockup; future Unity TMP Bold/outline must be matched visually.'
}));f:close()
print('Selected A retained. Header panel removed; bold white face and 4px dark outline exported.')
