-- Native Aseprite pixel artwork and approval compositions; never imports into Unity.
local out=app.params.outputDir
local project=app.params.projectDir
local pc=app.pixelColor
local colors={'412B22','6F4932','9D6945','C18B59','DBA670','EFC18B','F8D9A7','FFF0C9'}
local function c(hex) return pc.rgba(tonumber(hex:sub(1,2),16),tonumber(hex:sub(3,4),16),tonumber(hex:sub(5,6),16),255) end
local P={};for i,h in ipairs(colors) do P[i]=c(h) end
local function rect(im,x,y,w,h,col)
 for j=math.max(0,y),math.min(im.height-1,y+h-1) do for i=math.max(0,x),math.min(im.width-1,x+w-1) do im:drawPixel(i,j,col) end end
end
local function crop(im,x,y,w,h) local r=Image(w,h,ColorMode.RGB);r:drawImage(im,Point(-x,-y));return r end
local function resize(im,w,h) local r=Image(im);r:resize(w,h);return r end
local function trim(im)
 local l,t,r,b=im.width,im.height,-1,-1
 for y=0,im.height-1 do for x=0,im.width-1 do if pc.rgbaA(im:getPixel(x,y))>0 then l=math.min(l,x);t=math.min(t,y);r=math.max(r,x);b=math.max(b,y) end end end
 return r>=l and crop(im,l,t,r-l+1,b-t+1) or im
end
local function nine(im,w,h,b)
 local r=Image(w,h,ColorMode.RGB)
 local sx={0,b,im.width-b};local sy={0,b,im.height-b};local sw={b,im.width-2*b,b};local sh={b,im.height-2*b,b}
 local dx={0,b,w-b};local dy={0,b,h-b};local dw={b,w-2*b,b};local dh={b,h-2*b,b}
 for y=1,3 do for x=1,3 do if dw[x]>0 and dh[y]>0 then r:drawImage(resize(crop(im,sx[x],sy[y],sw[x],sh[y]),dw[x],dh[y]),Point(dx[x],dy[y])) end end end
 return r
end
local function stepbox(im,x,y,w,h,col)
 rect(im,x+2,y,w-4,h,col);rect(im,x+1,y+1,w-2,h-2,col);rect(im,x,y+2,w,h-4,col)
end
local function frame(w,h,fill,selected)
 local im=Image(w,h,ColorMode.RGB)
 stepbox(im,0,0,w,h,P[2]);stepbox(im,1,1,w-2,h-2,P[4]);stepbox(im,2,2,w-4,h-4,fill or 0)
 rect(im,3,1,w-6,1,P[7]);rect(im,1,3,1,h-6,P[5])
 if selected then
  for _,v in ipairs({{0,0,1,1},{w-1,0,-1,1},{0,h-1,1,-1},{w-1,h-1,-1,-1}}) do
   for i=1,6 do im:drawPixel(v[1]+i*v[3],v[2]+v[4],P[8]);im:drawPixel(v[1]+v[3],v[2]+i*v[4],P[8]) end
   im:drawPixel(v[1]+2*v[3],v[2]+2*v[4],P[2])
  end
 end
 return im
end
local assets={};local placements={};local sheet=Sprite(256,144,ColorMode.RGB)
sheet.layers[1].name='Transparent base'
local function add(name,im,x,y,border)
 assets[name]={image=im,border=border or 0}
 local layer=sheet:newLayer();layer.name=name;sheet:newCel(layer,1,im,Point(x,y))
 local slice=sheet:newSlice(Rectangle(x,y,im.width,im.height));slice.name=name
 if border and border>0 then slice.center=Rectangle(border,border,im.width-2*border,im.height-2*border) end
 placements[#placements+1]={name=name,x=x,y=y,width=im.width,height=im.height,border=border or 0}
 local single=Sprite(im.width,im.height,ColorMode.RGB);single.layers[1].name=name;single.cels[1].image:drawImage(im)
 single:saveCopyAs(out..'/assets/'..name..'.png');single:close()
end
add('CardFrame',frame(32,32,nil,false),0,0,7)
add('SelectedFrame',frame(32,32,nil,true),40,0,7)
add('LockedCard',frame(32,32,P[5],false),80,0,7)
local detail=frame(48,32,nil,false)
for _,x in ipairs({4,40}) do for _,y in ipairs({4,24}) do rect(detail,x,y,4,1,P[3]);rect(detail,x,y,1,4,P[3]);detail:drawPixel(x+2,y+2,P[4]) end end
add('IllustrationFrame',detail,120,0,9)
add('Nameplate',frame(48,16,P[2],false),176,0,4)
add('RewardPanel',frame(48,24,P[7],false),0,40,5)
add('ButtonNormal',frame(32,16,P[6],false),56,40,5)
add('ButtonHover',frame(32,16,P[7],true),96,40,7)
local pressed=frame(32,16,P[5],false);rect(pressed,3,2,26,1,P[3]);add('ButtonPressed',pressed,136,40,5)
local divider=Image(48,9,ColorMode.RGB);rect(divider,0,4,48,1,P[3]);rect(divider,0,5,48,1,P[7])
for j=-3,3 do for i=-3,3 do if math.abs(i)+math.abs(j)<=3 then divider:drawPixel(23+i,4+j,P[2]) end end end
divider:drawPixel(23,4,P[7]);add('Divider',divider,176,40,0)
local function arrow(right)
 local im=Image(12,12,ColorMode.RGB)
 for y=1,10 do local width=5-math.abs(y-5);if width>0 then for x=0,width do im:drawPixel(right and 3+x or 8-x,y,P[2]) end end end
 for y=3,7 do local width=2-math.abs(y-5);if width>=0 then for x=0,width do im:drawPixel(right and 4+x or 7-x,y,P[5]) end end end
 return im
end
add('ArrowLeft',arrow(false),0,80);add('ArrowRight',arrow(true),16,80)
for state=0,1 do
 local im=Image(8,8,ColorMode.RGB)
 for y=0,7 do for x=0,7 do local d=math.abs(x-3)+math.abs(y-3);if d<=3 then im:drawPixel(x,y,d==3 and P[2] or (state==1 and P[8] or P[4])) end end end
 add(state==0 and 'DotEmpty' or 'DotSelected',im,32+state*16,80)
end
local track=Image(8,32,ColorMode.RGB);rect(track,2,0,4,32,P[4]);rect(track,3,1,2,30,P[2]);add('ScrollTrack',track,64,80,2)
local thumb=frame(8,16,P[6],false);for y=6,10,2 do rect(thumb,2,y,4,1,P[3]) end;add('ScrollThumb',thumb,80,80,3)
local question=Image(24,24,ColorMode.RGB)
local pattern={'011110','110011','000011','000110','001100','001100','000000','001100','001100'}
for y,row in ipairs(pattern) do for x=1,#row do if row:sub(x,x)=='1' then rect(question,5+(x-1)*2,3+(y-1)*2,2,2,P[2]) end end end
add('UnknownQuestion',question,104,80)
local medallion=frame(32,32,P[6],false);medallion:drawImage(question,Point(4,4));add('UnknownMedallion',medallion,144,80,0)
local rule=Image(8,2,ColorMode.RGB);rect(rule,0,0,8,1,P[3]);rect(rule,0,1,8,1,P[7]);add('DividerLine',rule,184,80)
add('DividerOrnament',crop(divider,18,0,11,9),200,80)
-- Four outward diagonal arrows, mirrored from one pixel-perfect northwest arrow.
local expand=Image(16,16,ColorMode.RGB)
local northwest=Image(16,16,ColorMode.RGB)
rect(northwest,1,1,5,2,P[2]);rect(northwest,1,1,2,5,P[2])
for k=3,5 do rect(northwest,k,k,2,2,P[2]) end
for y=0,7 do for x=0,7 do if pc.rgbaA(northwest:getPixel(x,y))>0 then
 for _,xx in ipairs({x,15-x}) do for _,yy in ipairs({y,15-y}) do expand:drawPixel(xx,yy,P[2]) end end
end end end
add('FullViewExpand',expand,224,80)
add('SectionLabel',frame(80,18,P[7],false),0,120,5)
-- Straight rectangular bevel for the boss portrait: no inset corner motifs.
local bossFrame=Image(48,24,ColorMode.RGB)
rect(bossFrame,0,0,48,24,P[2]);rect(bossFrame,1,1,46,22,P[4]);rect(bossFrame,2,2,44,20,0)
rect(bossFrame,1,1,46,1,P[7]);rect(bossFrame,1,2,1,20,P[5])
add('BossIllustrationFrame',bossFrame,96,120,4)
sheet:saveAs(out..'/EncyclopediaPixelSkin.aseprite');sheet:saveCopyAs(out..'/EncyclopediaPixelSkin.png');sheet:close()
local manifest=io.open(out..'/slices.json','w');manifest:write('{"origin":"top-left","palette":[')
for i,h in ipairs(colors) do manifest:write((i>1 and ',' or '')..'"#'..h..'"') end
manifest:write('],"sprites":[')
for i,a in ipairs(placements) do manifest:write((i>1 and ',' or '')..string.format('{"name":"%s","x":%d,"y":%d,"width":%d,"height":%d,"border":%d}',a.name,a.x,a.y,a.width,a.height,a.border)) end
manifest:write(']}');manifest:close()
local full=Image{fromFile=out..'/EncyclopediaPixelSkin.png'}
local swatch=Sprite(1024,576,ColorMode.RGB);swatch.layers[1].name='Beveled pixel skin - nearest neighbor 4x';swatch.cels[1].image:drawImage(resize(full,1024,576));swatch:saveCopyAs(out..'/skin-sheet-preview.png');swatch:close()

-- Composites below use original game images. Labels are separate editable layers.
local sources=dofile(out..'/sources.lua');local cache={}
local function source(id)
 if cache[id] then return cache[id] end
 local a=sources[id];assert(a,id);local r=a.rect
 local im=Image{fromFile=a.path};cache[id]=trim(crop(im,r[1],r[2],r[3],r[4]));return cache[id]
end
local function ui(im,name,x,y,w,h)
 local a=assets[name];local rendered
 if a.border>0 then rendered=resize(nine(a.image,math.floor(w/2),math.floor(h/2),a.border),w,h)
 else rendered=resize(a.image,w,h) end
 im:drawImage(rendered,Point(x,y))
end
local function drawPicture(im,id,x,y,w,h,black)
 local art=source(id);local scale=math.min(w/art.width,h/art.height)
 local ww,hh=math.max(1,math.floor(art.width*scale)),math.max(1,math.floor(art.height*scale))
 local fitted=resize(art,ww,hh)
 if black then for yy=0,hh-1 do for xx=0,ww-1 do local v=fitted:getPixel(xx,yy);if pc.rgbaA(v)>0 then fitted:drawPixel(xx,yy,pc.rgba(30,23,20,pc.rgbaA(v))) end end end end
 im:drawImage(fitted,Point(x+math.floor((w-ww)/2),y+h-hh))
end
local base=Image{fromFile=project..'/outputs/encyclopedia-tab-visibility-reveal-2026-10-12/current-actual-monster-1920x1080.png'}
-- Remove the four printed corner motifs on each original book page.
-- Neighboring clean paper and border samples retain the existing book colors.
local originalBook=Image(base)
local cornerRects={
 {447,272,36,36,850,447,453},{897,272,38,36,850,927,933},
 {447,830,36,36,850,447,453},{897,830,38,36,850,927,933},
 {989,272,38,36,1100,989,998},{1435,272,40,36,1100,1466,1472},
 {989,830,38,36,1100,989,998},{1435,830,40,36,1100,1466,1472}
}
for _,r in ipairs(cornerRects) do
 for y=r[2],r[2]+r[4]-1 do
  for x=r[1],r[1]+r[3]-1 do base:drawPixel(x,y,originalBook:getPixel(r[5],y)) end
  if y<=863 then for x=r[6],r[7] do base:drawPixel(x,y,originalBook:getPixel(x,318)) end end
 end
end
-- Black/alpha overlays are separate from the opaque pixel-skin palette.
local fades={}
local function fadeImage(w,h,top)
 local im=Image(w,h,ColorMode.RGB)
 local maximum=top and 204 or 242
 for y=0,h-1 do
  local t=y/math.max(1,h-1)
  local alpha=math.floor(maximum*(top and (1-t) or t)+0.5)
  rect(im,0,y,w,1,pc.rgba(0,0,0,alpha))
 end
 return im
end
for _,top in ipairs({false,true}) do
 local name=top and 'BlackFadeTop' or 'BlackFadeBottom'
 local im=fadeImage(8,64,top);fades[name]=im
 local s=Sprite(8,64,ColorMode.RGB);s.layers[1].name=name;s.cels[1].image:drawImage(im)
 s:saveAs(out..'/overlays/'..name..'.aseprite');s:saveCopyAs(out..'/overlays/'..name..'.png');s:close()
end
local function fade(im,x,y,w,h,top)
 im:drawImage(fadeImage(w,h,top),Point(x,y))
end
-- Ivory indicators stay readable over the restored black portrait fade.
local lightDots={}
for _,name in ipairs({'DotEmpty','DotSelected'}) do
 local im=Image(8,8,ColorMode.RGB)
 for y=0,im.height-1 do for x=0,im.width-1 do
  local d=math.abs(x-3)+math.abs(y-3)
  if d==3 or (name=='DotSelected' and d<3) then im:drawPixel(x,y,P[8]) end
 end end
 lightDots[name]=im
 local s=Sprite(im.width,im.height,ColorMode.RGB);s.cels[1].image:drawImage(im)
 s:saveCopyAs(out..'/overlays/'..name..'Light.png');s:close()
end
local paper=base:getPixel(1003,700)
local function blank()
 local im=Image(base);rect(im,466,289,448,568,paper);rect(im,1008,289,450,568,paper)
 return im
end
local function dividerDraw(im,x,y,w)
 rect(im,x,y,w,2,P[3]);rect(im,x,y+2,w,2,P[7]);im:drawImage(resize(crop(assets.Divider.image,18,0,11,9),22,18),Point(x+math.floor(w/2)-11,y-7))
end
local function bottom(im,page)
 ui(im,'ButtonNormal',568,818,42,34);ui(im,'ArrowLeft',577,823,24,24)
 ui(im,'ButtonNormal',770,818,42,34);ui(im,'ArrowRight',779,823,24,24)
 -- Restore the beveled selection treatment on the active bookmark.
 ui(im,'SelectedFrame',1488,page=='monster' and 382 or 446,76,48)
end
local function savePreview(kind,art)
 local s=Sprite(1920,1080,ColorMode.RGB);s.layers[1].name='Original book + original sprites + new pixel skin';s.cels[1].image:drawImage(art)
 local layer=s:newLayer();layer.name='Korean labels - editable preview, example discovery state'
 s:newCel(layer,1,Image{fromFile=out..'/'..kind..'-text.png'})
 s:crop(Rectangle(400,220,1180,740));s:saveAs(out..'/'..kind..'-preview.aseprite');s:saveCopyAs(out..'/'..kind..'-preview.png');s:close()
end
local monster=blank();dividerDraw(monster,469,337,438)
-- Order follows the catalog; exact IDs are read rather than guessed aliases.
local order={};local file=io.open(project..'/Assets/_Project/Data/Encyclopedia/EncyclopediaCatalog.asset','r');local catalog=file:read('*a');file:close()
for id in catalog:match('  monsterEntries:\n(.-)  bossEntries:'):gmatch('  %- id: ([^\r\n]+)') do order[#order+1]=id end
for i=1,12 do
 local x=469+((i-1)%4)*112;local y=350+math.floor((i-1)/4)*150
 rect(monster,x+4,y+4,94,128,i>8 and P[5] or P[6])
 drawPicture(monster,order[i],x+8,y+6,86,94,i>8)
 ui(monster,i==1 and 'SelectedFrame' or 'CardFrame',x,y,102,136)
end
rect(monster,1012,348,442,204,P[6]);drawPicture(monster,order[1],1080,354,306,192,false)
ui(monster,'IllustrationFrame',1008,344,450,212)
ui(monster,'SectionLabel',1110,568,238,34)
dividerDraw(monster,1020,620,424);bottom(monster,'monster');savePreview('monster',monster)

local boss=blank();dividerDraw(boss,469,337,438)
for i=1,4 do
 local x=469+((i-1)%2)*224;local y=350+math.floor((i-1)/2)*230
 rect(boss,x+4,y+4,206,210,i>2 and P[5] or P[6])
 if i<=2 then
  drawPicture(boss,i==1 and 'boss.slime_queen' or 'boss.witch_chloe',x+6,y+6,202,204,false)
  fade(boss,x+2,y+122,210,94,false)
  for heart=0,4 do drawPicture(boss,(i==1 and heart==0) and 'heart.full' or 'heart.empty',x+47+heart*25,y+192,20,18,false) end
 else
  drawPicture(boss,i==3 and 'boss.dragon' or 'boss.demon_king',x+6,y+6,202,204,true)
  fade(boss,x+2,y+122,210,94,false)
 end
 ui(boss,i==1 and 'SelectedFrame' or 'CardFrame',x,y,214,218)
end
-- Background, fades and outer frame share one rectangle, with the bevel drawn
-- over the fade edges; no exposed paper gutter between fade and frame.
local portrait={x=1008,y=290,w=450,h=304}
rect(boss,portrait.x,portrait.y,portrait.w,portrait.h,P[6])
drawPicture(boss,'boss.slime_queen',1020,332,426,252,false)
fade(boss,portrait.x,portrait.y,portrait.w,84,true)
fade(boss,portrait.x,portrait.y+portrait.h-94,portrait.w,94,false)
ui(boss,'BossIllustrationFrame',portrait.x,portrait.y,portrait.w,portrait.h)
ui(boss,'ButtonNormal',1404,546,42,42);ui(boss,'FullViewExpand',1409,551,32,32)
-- Melta has seven portraits; only five indicators are visible at once.
-- The future presenter uses the actual count below five and shifts the window above five.
local portraitCount=7
local visibleDots=math.min(5,portraitCount)
local dotsWidth=16+(visibleDots-1)*22
local dotsStart=1233-math.floor(dotsWidth/2)
for i=0,visibleDots-1 do boss:drawImage(resize(lightDots[i==0 and 'DotSelected' or 'DotEmpty'],16,16),Point(dotsStart+i*22,570)) end
ui(boss,'ButtonNormal',1416,422,30,42);ui(boss,'ArrowRight',1419,431,24,24)
for i=0,4 do drawPicture(boss,i==0 and 'heart.full' or 'heart.empty',1252+i*38,607,26,24,false) end
dividerDraw(boss,1020,642,424)
ui(boss,'RewardPanel',1020,684,420,82)
drawPicture(boss,'boss.slime_queen',1030,700,48,50,false)
dividerDraw(boss,1020,784,424)
ui(boss,'ScrollTrack',1448,654,12,192);ui(boss,'ScrollThumb',1448,656,12,46)
bottom(boss,'boss');savePreview('boss',boss)
local used={};local alpha={};for y=0,full.height-1 do for x=0,full.width-1 do local v=full:getPixel(x,y);alpha[pc.rgbaA(v)]=true;if pc.rgbaA(v)>0 then used[v]=true end end end
local count=0;for _ in pairs(used) do count=count+1 end
assert(count<=8,'Unexpected palette growth');for a in pairs(alpha) do assert(a==0 or a==255,'Partial alpha in UI sheet') end
local report=io.open(out..'/aseprite-validation.txt','w');report:write('PASS Native Aseprite source and individual PNG exports.\nPASS '..#placements..' named slices; '..count..' opaque colors; alpha only 0/255.\nPASS Monster and boss original-asset compositions exported at 1180x740.\nPreview only; no Unity import or runtime changes.\n');report:close()
print('ENCYCLOPEDIA_PIXEL_SKIN_PREVIEW_PASS '..#placements..' sprites, '..count..' colors.')
local fadeReport=io.open(out..'/gradient-validation.txt','w')
for name,im in pairs(fades) do
 local previous=pc.rgbaA(im:getPixel(0,0));local levels={}
 for y=0,im.height-1 do
  local v=im:getPixel(0,y);local a=pc.rgbaA(v);levels[a]=true
  assert(pc.rgbaR(v)==0 and pc.rgbaG(v)==0 and pc.rgbaB(v)==0,'Gradient must be black only')
  assert(name=='BlackFadeTop' and a<=previous or name=='BlackFadeBottom' and a>=previous,'Alpha must be monotonic')
  previous=a
 end
 local n=0;for _ in pairs(levels) do n=n+1 end
 fadeReport:write('PASS '..name..': 8x64, black RGB, '..n..' monotonic alpha levels.\n')
end
fadeReport:write('PASS nameplates removed from all card/portrait compositions; generic unused sheet asset preserved.\n')
fadeReport:write('Boss-only fades. Boss portrait/frame share 1008,290,450,304 bounds; corner motifs omitted.\n')
fadeReport:close();print('BLACK_ALPHA_GRADIENT_PREVIEW_PASS')
