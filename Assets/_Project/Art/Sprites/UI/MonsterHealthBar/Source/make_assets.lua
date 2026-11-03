local out=app.params.outputDir
local pc=app.pixelColor
local function c(r,g,b) return pc.rgba(r,g,b,255) end
local ink=c(0,0,0)
local rim=c(143,140,145)
local dark=c(6,5,8)
local red=c(204,6,20)
local shine=c(255,43,59)
local white=c(255,255,255)
local function blank(w,h) return Image(w,h,ColorMode.RGB) end
local function rect(im,x,y,w,h,p)
 for yy=y,y+h-1 do for xx=x,x+w-1 do im:drawPixel(xx,yy,p) end end
end
local function frame(w)
 local im=blank(w,8)
 rect(im,1,0,w-2,8,ink);rect(im,0,1,w,6,ink)
 rect(im,2,1,w-4,6,rim);rect(im,1,2,w-2,4,rim)
 rect(im,2,2,w-4,4,0)
 return im
end
local function track(w)
 local im=blank(w,4);rect(im,0,0,w,4,dark);return im
end
local function fill(w)
 local im=blank(w,4);rect(im,0,0,w,4,red);rect(im,0,0,w,1,shine);return im
end
local function trail(w)
 local im=blank(w,4);rect(im,0,0,w,4,white);return im
end
local function composite(w,hp,damage)
 local im=blank(w,8);im:drawImage(track(w-4),Point(2,2))
 local tw=math.floor((w-4)*damage+0.5)
 if tw>0 then im:drawImage(trail(tw),Point(2,2)) end
 local fw=math.floor((w-4)*hp+0.5)
 if fw>0 then im:drawImage(fill(fw),Point(2,2)) end
 im:drawImage(frame(w))
 return im
end
local sprite=Sprite(40,8,ColorMode.RGB)
local layers={
 {'01_track',track(36),Point(2,2)},
 {'02_damage_trail',trail(36),Point(2,2)},
 {'03_health_fill',fill(36),Point(2,2)},
 {'04_frame',frame(40),Point(0,0)}
}
for i,item in ipairs(layers) do
 local layer=i==1 and sprite.layers[1] or sprite:newLayer()
 layer.name=item[1];sprite:newCel(layer,1,item[2],item[3])
 item[2]:saveAs(out..'/'..item[1]..'.png')
end
sprite.data='Pixel HP concept. Four static layers, six opaque colors. Frame40x8, inner36x4. Preserve pixel caps; clip fill/trail, do not stretch the entire assembled bar. No Unity integration performed.'
sprite:saveAs(out..'/monster_hp.aseprite');sprite:close()
local font={
 A={'010','101','111','101','101'},C={'111','100','100','100','111'},D={'110','101','101','101','110'},
 E={'111','100','110','100','111'},F={'111','100','110','100','100'},G={'111','100','101','101','111'},
 H={'101','101','111','101','101'},I={'111','010','010','010','111'},K={'101','101','110','101','101'},
 L={'100','100','100','100','111'},M={'101','111','111','101','101'},N={'101','111','111','111','101'},
 O={'111','101','101','101','111'},P={'110','101','110','100','100'},R={'110','101','110','101','101'},
 S={'111','100','111','001','111'},T={'111','010','010','010','010'},U={'101','101','101','101','111'},
 V={'101','101','101','101','010'},X={'101','101','010','101','101'},Y={'101','101','010','010','010'},
 ['0']={'111','101','101','101','111'},['1']={'010','110','010','010','111'},['2']={'110','001','010','100','111'},
 ['4']={'101','101','111','001','001'},['5']={'111','100','110','001','110'},['6']={'111','100','111','101','111'},
 ['8']={'111','101','111','101','111'},['-']={'000','000','111','000','000'}
}
local function label(im,text,x,y,scale)
 for i=1,#text do
  local glyph=font[text:sub(i,i)]
  if glyph then for yy=1,5 do for xx=1,3 do if glyph[yy]:sub(xx,xx)=='1' then
   rect(im,x+(xx-1)*scale,y+(yy-1)*scale,scale,scale,c(216,213,222))
  end end end end
  x=x+4*scale
 end
end
local function zoom(im,scale)
 local z=Image(im);z:resize(im.width*scale,im.height*scale);return z
end
local board=blank(704,400)
for y=0,399 do for x=0,703 do
 local v=(math.floor(x/16)+math.floor(y/16))%2
 board:drawPixel(x,y,v==0 and c(32,34,41) or c(35,37,44))
end end
label(board,'MONSTER HP - PIXEL ASSETS',24,18,3)
label(board,'ASEPRITE - 6 COLORS - CONCEPT',24,45,2)
label(board,'FULL HP',24,82,2)
board:drawImage(zoom(composite(40,1,1),6),Point(24,99))
label(board,'HALF HP',24,164,2)
board:drawImage(zoom(composite(40,0.5,0.5),6),Point(24,181))
label(board,'DAMAGE TRAIL',24,246,2)
board:drawImage(zoom(composite(40,0.35,0.75),6),Point(24,263))
label(board,'SEPARATE STATIC PARTS',344,82,2)
local names={'FRAME','TRACK','HEALTH FILL','DAMAGE TRAIL'}
local parts={frame(40),track(36),fill(36),trail(36)}
for i,part in ipairs(parts) do
 label(board,names[i],344,107+(i-1)*56,2)
 board:drawImage(zoom(part,5),Point(344,123+(i-1)*56))
end
label(board,'NATIVE 40 X 8',24,346,2)
board:drawImage(composite(40,0.65,0.9),Point(154,347))
label(board,'PREVIEW 6X - NOT GAME SCALE',24,374,2)
board:saveAs(out..'/preview.png')
local palette={[ink]=true,[rim]=true,[dark]=true,[red]=true,[shine]=true,[white]=true}
local seen={}
for _,part in ipairs(parts) do for y=0,part.height-1 do for x=0,part.width-1 do
 local p=part:getPixel(x,y)
 if pc.rgbaA(p)>0 then assert(pc.rgbaA(p)==255 and palette[p],'Unexpected pixel color');seen[p]=true end
end end end
local count=0;for _ in pairs(seen) do count=count+1 end
assert(count==6,'Expected six opaque colors')
local file=io.open(out..'/asset-notes.txt','w')
file:write('Aseprite-native pixel HP concept v1.\nFour static PNG components and layered .aseprite source.\nFrame:40x8, track/fill/trail:36x4; placement inside frame:(2,2).\nSix opaque colors:000000 8F8C91 060508 CC0614 FF2B3B FFFFFF.\nAll source alpha values are 0 or255; no blur or antialiasing.\nPreview is6x enlarged; component panel5x. Native example also included.\nUse a2px frame border for horizontal cap preservation. Fill/trail are clipped by current/trailing HP, never selected from animation frames.\nCurrent monster HUD dimensions are Unity layout units, not these source pixel dimensions. Final world scale requires gameplay-camera review.\nA texture-sampling extension of the current shared renderer can retain its batching architecture; the overlap Image fallback needs the same artwork. Performance and native sorting validation are required during integration.\nNo Unity project assets, prefabs, renderer, combat logic, or documents were changed.\nValidation: Aseprite export succeeded; all four source images use only the six specified opaque colors.\n')
file:close()
