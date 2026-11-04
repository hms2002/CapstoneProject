-- Native pixel authoring in Aseprite. Re-run with projectDir/outputDir parameters.
-- Originals are read-only; everything is saved beside this script, outside Assets.
local root=assert(app.params.projectDir)
local out=assert(app.params.outputDir)
local pc=app.pixelColor
local function rgb(h) return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255) end
local function hex(p) return string.format('%02X%02X%02X',pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p)) end
local function rect(im,x,y,w,h,c)
 for yy=math.max(y,0),math.min(y+h-1,im.height-1) do
  for xx=math.max(x,0),math.min(x+w-1,im.width-1) do im:drawPixel(xx,yy,c) end
 end
end
local function dot(im,x,y,c) rect(im,x,y,1,1,c) end
local function line(im,x0,y0,x1,y1,c)
 local dx,dy=math.abs(x1-x0),-math.abs(y1-y0)
 local sx,sy=x0<x1 and 1 or -1,y0<y1 and 1 or -1
 local e=dx+dy
 while true do
  dot(im,x0,y0,c);if x0==x1 and y0==y1 then break end
  local e2=2*e;if e2>=dy then e=e+dy;x0=x0+sx end;if e2<=dx then e=e+dx;y0=y0+sy end
 end
end
local function poly(im,pts,c)
 local miny,maxy=im.height,0
 for _,p in ipairs(pts) do miny=math.min(miny,p[2]);maxy=math.max(maxy,p[2]) end
 for y=miny,maxy do
  local xs={}
  for i,a in ipairs(pts) do local b=pts[i%#pts+1]
   if (a[2]<=y and b[2]>y) or (b[2]<=y and a[2]>y) then xs[#xs+1]=a[1]+(y-a[2])*(b[1]-a[1])/(b[2]-a[2]) end
  end
  table.sort(xs)
  for i=1,#xs-1,2 do rect(im,math.ceil(xs[i]),y,math.floor(xs[i+1])-math.ceil(xs[i])+1,1,c) end
 end
end
local function crop(im,x,y,w,h) return Image(im,Rectangle(x,y,w,h)) end
-- Explicit integer replication: no resampling or antialiasing anywhere.
local function enlarged(im,n)
 local res=Image(im.width*n,im.height*n,ColorMode.RGB)
 for y=0,im.height-1 do for x=0,im.width-1 do rect(res,x*n,y*n,n,n,im:getPixel(x,y)) end end
 return res
end
local function composite(images,w,h)
 local im=Image(w,h,ColorMode.RGB);for _,layer in ipairs(images) do im:drawImage(layer) end;return im
end
local function addLayer(sprite,name,im)
 local layer=sprite:newLayer();layer.name=name;sprite:newCel(layer,1,im,Point(0,0));return layer
end
local function save(im,name) im:saveAs(out..'/exports/'..name..'.png') end
local worldSource=Image{fromFile=root..'/Assets/_Project/Art/Sprites/Map/Environment/Level/All Chest Sheet.png'}
local uiAtlas=Image{fromFile=root..'/Assets/_Project/Art/Sprites/UI/Chest/examples.png'}
local inventory=Image{fromFile=root..'/Assets/_Project/Art/Sprites/UI/Chest/UI Chest inventory.png'}
local uiSource=crop(uiAtlas,384,20,80,122)
local colorSet={}
for _,im in ipairs({worldSource,uiAtlas,inventory}) do
 for p in im:pixels() do if pc.rgbaA(p())==255 then colorSet[p()]=true end end
end
local function existing(h)
 local target=rgb(h);if colorSet[target] then return target end
 local best,d=nil,math.huge
 for c in pairs(colorSet) do
  local delta=(pc.rgbaR(c)-pc.rgbaR(target))^2+(pc.rgbaG(c)-pc.rgbaG(target))^2+(pc.rgbaB(c)-pc.rgbaB(target))^2
  if delta<d then best,d=c,delta end
 end
 return best
end
local C={ink=rgb('181425'),dark=rgb('262B44'),steel=rgb('92A1B9'),light=rgb('C7CFDD'),white=rgb('FFFFFF'),
 wood=rgb('A57855'),woodDark=rgb('734C44'),rim=rgb('BCAD9F'),iron=rgb('87857C'),ironDark=rgb('636663'),
 mouth=rgb('593E47'),lip=rgb('7A5859'),well=rgb('303843'),slot=rgb('405273'),
 eye=existing('FEAE34'),eyeLight=existing('FEE761'),tongue=existing('B75D48'),tongueLight=existing('D77643')}
local function setPalette(sprite,images)
 local seen,list={},{}
 for _,im in ipairs(images) do for p in im:pixels() do local c=p();if pc.rgbaA(c)>0 and not seen[c] then seen[c]=true;list[#list+1]=c end end end
 table.sort(list,function(a,b)return hex(a)<hex(b) end)
 local pal=Palette(#list+1);pal:setColor(0,Color{r=0,g=0,b=0,a=0})
 for i,c in ipairs(list) do pal:setColor(i,Color{r=pc.rgbaR(c),g=pc.rgbaG(c),b=pc.rgbaB(c),a=255}) end
 sprite:setPalette(pal)
 return list
end

-- WORLD: use original frames and metal/green body pixels at their original size.
local world=Sprite(16,16,ColorMode.RGB)
world.layers[1].name='01 Original chest body - 16px'
local face=world:newLayer();face.name='02 Eyes and mouth'
local fangs=world:newLayer();fangs.name='03 One-pixel teeth'
local tongue=world:newLayer();tongue.name='04 Tongue'
local worldImages={}
local function worldFrame(frame,sourceIndex,stage,wag)
 if frame>1 then world:newEmptyFrame(frame) end
 local base=crop(worldSource,sourceIndex*16,0,16,16)
 local eyes,teeth,tong=Image(16,16),Image(16,16),Image(16,16)
 if stage==1 then
  rect(eyes,4,5,8,3,C.ink)
  rect(eyes,5,6,2,1,C.eye);rect(eyes,9,6,2,1,C.eye)
  dot(eyes,5,6,C.eyeLight);dot(eyes,10,6,C.eyeLight)
 elseif stage==2 then
  rect(eyes,4,6,8,3,C.ink)
  rect(eyes,5,6,2,1,C.eye);rect(eyes,9,6,2,1,C.eye)
  dot(teeth,4,8,C.light);dot(teeth,11,8,C.light)
 elseif stage>=3 then
  local top=stage==3 and 5 or 3
  rect(eyes,3,top,10,10-top,C.ink)
  rect(eyes,4,top+2,8,5-top+3,C.mouth)
  rect(eyes,4,top-2,3,2,C.ink);rect(eyes,9,top-2,3,2,C.ink)
  rect(eyes,5,top-1,2,1,C.eye);rect(eyes,9,top-1,2,1,C.eye)
  dot(eyes,5,top-1,C.eyeLight);dot(eyes,10,top-1,C.eyeLight)
  for _,x in ipairs({3,6,9,12}) do dot(teeth,x,top+1,C.light);if x==3 or x==12 then dot(teeth,x,top+2,C.rim) end end
  for _,x in ipairs({4,7,10}) do dot(teeth,x,9,C.light) end
  if stage>=4 then
   rect(tong,7,8,2,3,C.tongue);dot(tong,7,8,C.tongueLight)
   rect(tong,8,11,2+(wag or 0),2,C.tongue);dot(tong,8,11,C.tongueLight)
   dot(tong,10+(wag or 0),11,C.lip)
  end
 end
 world:newCel(world.layers[1],frame,base,Point(0,0))
 world:newCel(face,frame,eyes,Point(0,0));world:newCel(fangs,frame,teeth,Point(0,0));world:newCel(tongue,frame,tong,Point(0,0))
 world.frames[frame].duration=frame==1 and 0.7 or (frame==2 and 0.35 or (frame>=11 and 0.22 or 1/12))
 worldImages[frame]=composite({base,eyes,teeth,tong},16,16)
end
worldFrame(1,0,0);worldFrame(2,0,1)
for i=0,7 do worldFrame(i+3,i,i==0 and 1 or (i<3 and 2 or (i==3 and 3 or 4))) end
worldFrame(11,7,4,0);worldFrame(12,7,4,1)
for _,t in ipairs({{'Closed',1,1},{'Awake',2,2},{'Open',3,10},{'Opened',11,12}}) do local tag=world:newTag(t[2],t[3]);tag.name=t[1] end
world.gridBounds=Rectangle(0,0,1,1)
world.data='Native 16x16. Opening frames 3-10: 12 fps, based on original 8 poses. Suggested future Unity PPU 16, pivot (0.5,0.123). Not imported.'
local worldColors=setPalette(world,worldImages)
world:saveAs(out..'/MimicChest-16px.aseprite')
local worldSheet=Image(192,16)
for i,im in ipairs(worldImages) do worldSheet:drawImage(im,Point((i-1)*16,0)) end
save(worldSheet,'MimicChest-all-frames-192x16')
local opening=Image(128,16)
for i=3,10 do opening:drawImage(worldImages[i],Point((i-3)*16,0)) end
save(opening,'MimicChest-open-128x16')
save(worldImages[1],'MimicChest-closed');save(worldImages[2],'MimicChest-awake');save(worldImages[11],'MimicChest-opened')
world:saveCopyAs(out..'/exports/MimicChest-native.gif')
local anim=Sprite(128,128)
for i,im in ipairs(worldImages) do
 if i>1 then anim:newEmptyFrame(i) end
 anim:newCel(anim.layers[1],i,enlarged(im,8),Point(0,0));anim.frames[i].duration=world.frames[i].duration
end
anim:saveCopyAs(out..'/MimicChest-preview-8x.gif');anim:close()

-- UI: same 80x122 source footprint; original wood/metal remain on a base layer.
local ui=Sprite(80,122)
ui.gridBounds=Rectangle(0,0,1,1)
ui.layers[1].name='01 Original wood and iron frame'
local base=Image(uiSource)
rect(base,4,44,72,38,C.well) -- remove only the illustrative slot blocks.
ui:newCel(ui.layers[1],1,base,Point(0,0))
local mouth=Image(80,122)
rect(mouth,4,44,72,38,C.mouth)
rect(mouth,6,47,68,32,C.ink)
rect(mouth,5,46,70,2,C.lip);rect(mouth,5,78,70,3,C.mouth)
local eyes=Image(80,122)
poly(eyes,{{14,22},{30,27},{30,31},{17,29},{14,26}},C.mouth)
poly(eyes,{{65,22},{49,27},{49,31},{62,29},{65,26}},C.mouth)
poly(eyes,{{16,24},{29,28},{29,30},{18,28}},C.ink)
poly(eyes,{{63,24},{50,28},{50,30},{61,28}},C.ink)
rect(eyes,23,27,4,2,C.eye);dot(eyes,24,27,C.eyeLight);dot(eyes,26,28,C.woodDark)
rect(eyes,53,27,4,2,C.eye);dot(eyes,55,27,C.eyeLight);dot(eyes,53,28,C.woodDark)
local teeth=Image(80,122)
for i,x in ipairs({7,17,27,37,47,57,67}) do
 local height=(i==1 or i==7) and 7 or (i%2==0 and 5 or 4)
 for y=0,height-1 do
  local half=math.max(0,2-math.floor(y*3/height))
  rect(teeth,x+2-half,41+y,half*2+1,1,C.rim)
  if half>0 then dot(teeth,x+2-half,41+y,C.light);dot(teeth,x+2+half,41+y,C.iron) end
 end
end
for i,x in ipairs({9,21,32,48,59,69}) do
 local height=(i==1 or i==6) and 6 or 4
 for y=0,height-1 do
  local half=math.max(0,2-math.floor(y*3/height))
  rect(teeth,x-half,81-y,half*2+1,1,C.rim)
  if half>0 then dot(teeth,x-half,81-y,C.light);dot(teeth,x+half,81-y,C.iron) end
 end
end
local uiTongue=Image(80,122)
poly(uiTongue,{{37,79},{42,78},{46,81},{46,87},{44,92},{46,95},{49,94},{51,91},{52,93},{51,98},{46,100},{40,98},{37,94},{38,87},{36,83}},C.mouth)
poly(uiTongue,{{38,80},{42,79},{45,82},{44,89},{42,93},{45,97},{49,95},{50,94},{49,97},{46,98},{41,96},{39,93},{40,87},{37,83}},C.tongue)
line(uiTongue,39,80,42,82,C.tongueLight);line(uiTongue,42,82,41,89,C.tongueLight)
line(uiTongue,41,92,43,95,C.lip)
addLayer(ui,'02 Mouth interior',mouth);addLayer(ui,'03 Eyes',eyes);addLayer(ui,'04 Teeth',teeth);addLayer(ui,'05 Tongue',uiTongue)
local uiArt=composite({base,mouth,eyes,teeth,uiTongue},80,122)
local uiFrame=composite({base,mouth},80,122)
save(uiArt,'MimicChestUI-80x122');save(uiFrame,'MimicChestUI-frame')
save(eyes,'MimicChestUI-eyes');save(teeth,'MimicChestUI-teeth');save(uiTongue,'MimicChestUI-tongue')

-- Native artwork slices retain the exact dimensions/coordinates of examples_0..9.
local partDefs={{0,36,0,8,43},{1,44,0,23,43},{2,67,82,13,40},{3,0,82,13,40},{4,13,0,23,43},
 {5,13,82,54,40},{6,67,0,13,43},{7,0,0,13,43},{8,0,44,13,38},{9,67,44,13,38}}
local manifest={world={width=16,height=16,ppu=16,pivot={x=0.5,y=0.123},openFrames={first=3,last=10,fps=12}},
 ui={width=80,height=122,sourceRegion={x=384,y=20,width=80,height=122},parts={}},palette={}}
for _,p in ipairs(partDefs) do
 local name='MimicFrame-'..p[1]
 local slice=ui:newSlice(Rectangle(p[2],p[3],p[4],p[5]));slice.name='examples_'..p[1]..' - '..name
 save(crop(uiFrame,p[2],p[3],p[4],p[5]),name)
 manifest.ui.parts[#manifest.ui.parts+1]={name=name,sourceSprite='examples_'..p[1],x=p[2],y=p[3],width=p[4],height=p[5]}
end
-- Preview controls are a separately hidden reference layer, not baked into exports.
local controls=Image(80,122)
local function slot(x,y,w)
 rect(controls,x+1,y,w-2,w,C.steel);rect(controls,x,y+1,w,w-2,C.steel)
 rect(controls,x+1,y+1,w-2,w-2,C.well);rect(controls,x+2,y+2,w-4,w-4,C.dark)
 rect(controls,x+2,y+1,w-4,1,C.light);rect(controls,x+w-2,y+2,1,w-4,C.ironDark)
end
for _,x in ipairs({7,24,41,58}) do slot(x,55,14) end
-- Restrained 1px demo glyphs: sword, gem, bottle, ring.
line(controls,12,64,16,58,C.light);line(controls,13,64,17,58,C.steel);line(controls,11,63,15,65,C.eye);dot(controls,11,66,C.wood)
poly(controls,{{30,58},{33,61},{30,66},{27,61}},existing('63C74D'));line(controls,30,58,30,64,existing('3E8948'));dot(controls,29,60,C.light)
rect(controls,46,58,4,2,C.wood);rect(controls,45,61,6,5,C.steel);rect(controls,46,62,4,3,existing('0099DB'));dot(controls,46,61,C.white)
rect(controls,62,60,6,5,C.eye);rect(controls,63,61,4,3,C.dark);rect(controls,64,58,2,2,C.tongue);dot(controls,62,60,C.eyeLight)
slot(10,104,12);slot(25,104,12)
rect(controls,44,104,27,12,C.woodDark);rect(controls,45,105,25,10,C.mouth);rect(controls,46,105,23,1,C.rim)
line(controls,54,110,57,113,C.light);line(controls,57,113,63,107,C.light)
local controlsLayer=addLayer(ui,'06 PREVIEW ONLY - slots and button (hidden)',controls)
controlsLayer.isVisible=false
local uiColors=setPalette(ui,{uiArt,controls})
ui.data='Native 80x122; wood/iron preserved from examples.png. Expression layers remain separate. Hidden sample controls are not production UI. Slice bounds match examples_0..9; expression overlays still require approved Unity authoring.'
ui:saveAs(out..'/MimicChestUI-80x122.aseprite')
local uiPreview=composite({uiArt,controls},80,122)
save(uiPreview,'MimicChestUI-layout-preview')
enlarged(uiPreview,6):saveAs(out..'/MimicChestUI-preview-6x.png')

-- Comparison sheet is also a real editable, native-resolution Aseprite document.
local glyphs={
 A={'01110','10001','10001','11111','10001','10001','10001'},B={'11110','10001','10001','11110','10001','10001','11110'},
 C={'01111','10000','10000','10000','10000','10000','01111'},D={'11110','10001','10001','10001','10001','10001','11110'},
 E={'11111','10000','10000','11110','10000','10000','11111'},F={'11111','10000','10000','11110','10000','10000','10000'},
 G={'01111','10000','10000','10111','10001','10001','01110'},H={'10001','10001','10001','11111','10001','10001','10001'},
 I={'111','010','010','010','010','010','111'},K={'10001','10010','10100','11000','10100','10010','10001'},
 L={'10000','10000','10000','10000','10000','10000','11111'},M={'10001','11011','10101','10101','10001','10001','10001'},
 N={'10001','11001','10101','10011','10001','10001','10001'},O={'01110','10001','10001','10001','10001','10001','01110'},
 P={'11110','10001','10001','11110','10000','10000','10000'},R={'11110','10001','10001','11110','10100','10010','10001'},
 S={'01111','10000','10000','01110','00001','00001','11110'},T={'11111','00100','00100','00100','00100','00100','00100'},
 U={'10001','10001','10001','10001','10001','10001','01110'},V={'10001','10001','10001','10001','10001','01010','00100'},
 W={'10001','10001','10001','10101','10101','11011','10001'},X={'10001','10001','01010','00100','01010','10001','10001'},
 Y={'10001','10001','01010','00100','00100','00100','00100'},Z={'11111','00001','00010','00100','01000','10000','11111'},
 ['0']={'01110','10001','10011','10101','11001','10001','01110'},['1']={'010','110','010','010','010','010','111'},
 ['2']={'01110','10001','00001','00010','00100','01000','11111'},['3']={'11110','00001','00001','01110','00001','00001','11110'},
 ['4']={'10010','10010','10010','11111','00010','00010','00010'},['6']={'01110','10000','10000','11110','10001','10001','01110'},
 ['8']={'01110','10001','10001','01110','10001','10001','01110'},['/']={'00001','00001','00010','00100','01000','10000','10000'},
 ['-']={'000','000','000','111','000','000','000'}}
local function label(im,text,x,y,c)
 for i=1,#text do local ch=text:sub(i,i);local g=glyphs[ch]
  if g then for yy,row in ipairs(g) do for xx=1,#row do if row:sub(xx,xx)=='1' then dot(im,x+xx-1,y+yy-1,c) end end end;x=x+#g[1]+1 else x=x+4 end
 end
end
local board=Image(384,240)
rect(board,0,0,384,240,C.ink);rect(board,0,0,384,28,C.dark)
label(board,'MIMIC / NATIVE PIXEL ART',12,10,C.light)
label(board,'WORLD 16X16',12,37,C.rim);label(board,'UI 80X122',200,37,C.rim)
label(board,'ORIGINAL',12,53,C.steel);label(board,'MIMIC',104,53,C.eye)
board:drawImage(enlarged(crop(worldSource,0,0,16,16),4),Point(12,66))
board:drawImage(enlarged(worldImages[11],4),Point(104,66))
label(board,'ORIGINAL',200,53,C.steel);label(board,'MIMIC',294,53,C.eye)
board:drawImage(uiSource,Point(200,66));board:drawImage(uiPreview,Point(294,66))
label(board,'CLOSED / AWAKE / OPEN',12,141,C.steel)
for i,f in ipairs({1,2,11}) do board:drawImage(enlarged(worldImages[f],2),Point(12+(i-1)*54,154)) end
label(board,'8 OPEN FRAMES / 12 FPS',12,199,C.rim)
board:drawImage(opening,Point(12,212))
label(board,'SOURCE PALETTE',200,199,C.rim)
local swatch={C.ink,C.dark,C.steel,C.light,C.woodDark,C.wood,C.rim,C.mouth,C.lip,C.tongue,C.eye,C.eyeLight}
for i,c in ipairs(swatch) do rect(board,200+(i-1)*14,213,10,10,c) end
local sheet=Sprite(384,240);sheet.layers[1].name='Native comparison board';sheet:newCel(sheet.layers[1],1,board,Point(0,0))
setPalette(sheet,{board});sheet:saveAs(out..'/MimicChest-comparison.aseprite');sheet:close()
board:saveAs(out..'/MimicChest-comparison-native.png')
enlarged(board,3):saveAs(out..'/MimicChest-comparison-3x.png')

-- Record artifact facts, including source-palette-only and binary alpha.
local used={};local count=0;local partial=0;local foreign=0
for _,im in ipairs({uiArt,worldSheet}) do for p in im:pixels() do
 local c=p();local a=pc.rgbaA(c)
 if a>0 and a<255 then partial=partial+1 end
 if a>0 then if not used[c] then used[c]=true;count=count+1 end;if not colorSet[c] then foreign=foreign+1 end end
end end
for name,c in pairs(C) do manifest.palette[name]=hex(c) end
manifest.artifactFacts={authoringApp=tostring(app.version),worldFrames=#worldImages,worldColors=#worldColors,uiColors=#uiColors,
 combinedArtColors=count,partialAlphaPixels=partial,pixelsOutsideSourcePalette=foreign,unityImported=false}
local file=assert(io.open(out..'/asset-manifest.json','w'));file:write(json.encode(manifest));file:close()
world:close();ui:close()
