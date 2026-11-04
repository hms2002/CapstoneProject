-- Run with the installed Aseprite --batch --script-param root=... --script-param output=...
-- Artwork is authored in Aseprite. All game files are read-only inputs.
local root, out = app.params.root, app.params.output
local pc = app.pixelColor
local rgba = pc.rgba
local src = Image{fromFile=root..'/Assets/_Project/Art/Sprites/Map/Environment/Level/Portal.png'}
local W,H = src.width,src.height
local function color(h,a)
  return rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),a or 255)
end
local function rect(im,x,y,w,h,c)
  for yy=math.max(0,y),math.min(im.height-1,y+h-1) do
    for xx=math.max(0,x),math.min(im.width-1,x+w-1) do im:drawPixel(xx,yy,c) end
  end
end
local function scaled(im,n)
  local r=Image(im.width*n,im.height*n)
  for p in im:pixels() do rect(r,p.x*n,p.y*n,n,n,p()) end
  return r
end
local glyph={
 A='010101111101101',B='110101110101110',C='011100100100011',D='110101101101110',
 E='111100110100111',F='111100110100100',G='011100101101011',H='101101111101101',
 I='111010010010111',J='001001001101010',K='101101110101101',L='100100100100111',
 M='101111111101101',N='101111111111101',O='010101101101010',P='110101110100100',
 Q='010101101111011',R='110101110101101',S='011100010001110',T='111010010010010',
 U='101101101101111',V='101101101101010',W='101101111111101',X='101101010101101',
 Y='101101010010010',Z='111001010100111',
 ['0']='111101101101111',['1']='010110010010111',['2']='110001010100111',
 ['3']='110001010001110',['4']='101101111001001',['5']='111100110001110',
 ['6']='011100111101111',['7']='111001010010010',['8']='111101111101111',
 ['9']='111101111001110',['#']='101111101111101',['/']='001001010100100',
 ['-']='000000111000000',['.']='000000000000010',[':']='000010000010000',
 ['%']='101001010100101',['+']='000010111010000'
}
local function text(im,s,x,y,c,n)
  n=n or 1
  for i=1,#s do
    local g=glyph[s:sub(i,i)]
    if g then for j=1,15 do if g:sub(j,j)=='1' then
      rect(im,x+((j-1)%3)*n,y+math.floor((j-1)/3)*n,n,n,c)
    end end end
    x=x+4*n
  end
end
local variants={
 {id='00-original',name='ORIGINAL',theme='CURRENT VIOLET',mem='7F63DC',rim='8E6CFF',p1='8E6CFF',p2='B69FFF'},
 {id='01-chloe',name='CHLOE',theme='SHADOW / SMOKY',mem='79749B',rim='B7ACC9',p1='998CB6',p2='DAD0E8'},
 {id='02-spiri',name='SPIRI',theme='DRAGON / AMBER',mem='D99A3F',rim='FFCA68',p1='FFAF4D',p2='FFE6A8'},
 {id='03-melta',name='MELTA',theme='SLIME / JADE',mem='38C9A3',rim='6CF3D0',p1='43DBB1',p2='B9FFE8'},
 {id='04-demon-king',name='DEMON KING',theme='DARK / AMETHYST',mem='9C55DC',rim='C47CFF',p1='B66AFF',p2='EBC2FF'}
}
local stone=Image(W,H)
local maskCount,rimCount=0,0
for p in src:pixels() do
  if pc.rgbaA(p())==129 then maskCount=maskCount+1
  elseif p()==rgba(142,108,255,255) then rimCount=rimCount+1
  else stone:drawPixel(p.x,p.y,p()) end
end
local function parts(v)
  local membrane,edge=Image(W,H),Image(W,H)
  for p in src:pixels() do
    if pc.rgbaA(p())==129 then membrane:drawPixel(p.x,p.y,color(v.mem,129))
    elseif p()==rgba(142,108,255,255) then edge:drawPixel(p.x,p.y,color(v.rim)) end
  end
  return membrane,edge
end
local function floorImage(w,h,light)
  local im=Image(w,h)
  for y=0,h-1 do for x=0,w-1 do
    local row=math.floor(y/8)
    local seam=y%8==0 or (x+(row%2)*8)%16==0
    local hcol=light and (seam and 'A5ADB8' or 'CBD0D8') or (seam and '202633' or '2D3543')
    im:drawPixel(x,y,color(hcol))
  end end
  return im
end
-- A shared, deliberately simple particle layout isolates color differences.
-- This is a palette/motion study, not a simulation of the Unity ParticleSystem.
local function particles(v,f)
  local im=Image(60,64)
  for i=0,15 do
    local age=((f-1)+i*5)%24/24
    local a=math.floor(255*math.min(1,age*5,(1-age)*5))
    local side=i%2==0 and -1 or 1
    local x=math.floor(30+side*(8+5*math.sin(i*1.9+age*3.14)))
    local y=math.floor(57-age*42-(i%3))
    local size=(i%4==0 and age<.75) and 2 or 1
    rect(im,x,y,size,size,color(i%3==0 and v.p2 or v.p1,a))
  end
  return im
end
local function addLayer(sp,name)
  local l=sp:newLayer();l.name=name;return l
end
local preview={}
for vi,v in ipairs(variants) do
  local membrane,edge=parts(v)
  local sp=Sprite(W,H,ColorMode.RGB)
  sp.layers[1].name='Stone frame - original pixels'
  sp:newCel(sp.layers[1],1,stone,Point(0,0))
  sp:newCel(addLayer(sp,'Membrane - alpha 129 of 255'),1,membrane,Point(0,0))
  sp:newCel(addLayer(sp,'Inner energy rim'),1,edge,Point(0,0))
  sp:saveAs(out..'/'..v.id..'-portal.aseprite')
  Image(sp):saveAs(out..'/'..v.id..'-portal.png')
  membrane:saveAs(out..'/'..v.id..'-membrane.png')
  local pal=Palette(5)
  for i,h in ipairs({v.mem,v.rim,v.p1,v.p2,'FFFFFF'}) do pal:setColor(i-1,Color(color(h))) end
  sp:setPalette(pal)
  sp:saveAs(out..'/'..v.id..'-portal.aseprite')
  local anim=Sprite(60,64,ColorMode.RGB)
  anim.layers[1].name='Floor - preview only'
  local ml=addLayer(anim,'Membrane - alpha 129 of 255')
  local sl=addLayer(anim,'Stone frame - original pixels')
  local el=addLayer(anim,'Inner energy rim')
  local pl=addLayer(anim,'Particles - color study')
  anim:setPalette(pal)
  preview[vi]={}
  for f=1,24 do
    if f>1 then anim:newEmptyFrame() end
    anim.frames[f].duration=.1
    anim:newCel(anim.layers[1],f,floorImage(60,64,false),Point(0,0))
    anim:newCel(ml,f,membrane,Point(5,7))
    anim:newCel(sl,f,stone,Point(5,7))
    anim:newCel(el,f,edge,Point(5,7))
    anim:newCel(pl,f,particles(v,f),Point(0,0))
    local frame=Image(60,64);frame:drawSprite(anim,f)
    preview[vi][f]=frame
  end
  anim:newTag(1,24).name='Color study - 2.4 second loop'
  anim:saveAs(out..'/'..v.id..'-preview.aseprite')
  anim:saveCopyAs(out..'/'..v.id..'-preview.gif')
  sp:close();anim:close()
end
-- Comparison board: editable Aseprite layers, same lighting and motion in every column.
local BW,BH=680,412
local board=Sprite(BW,BH,ColorMode.RGB)
board.layers[1].name='Layout and palette labels'
local bg=Image(BW,BH);rect(bg,0,0,BW,BH,color('121721'))
text(bg,'PORTAL / BOSS COLOR STUDY',20,18,color('F1F0E9'),3)
text(bg,'ORIGINAL FRAME + TRANSLUCENT MEMBRANE + MATCHED PARTICLES',20,43,color('8D9BAD'))
rect(bg,20,57,640,1,color('354050'))
for vi,v in ipairs(variants) do
  local x=20+(vi-1)*130
  rect(bg,x,68,120,316,color('1B2230'))
  rect(bg,x,68,120,2,color(v.rim))
  text(bg,v.name,x+7,79,color('ECEFF4'),2)
  text(bg,v.theme,x+7,94,color('9BA8BB'))
  text(bg,'MEMBRANE / 51%',x+8,246,color('8999AD'))
  rect(bg,x+8,257,16,10,color(v.mem));text(bg,'#'..v.mem,x+30,260,color('E1E5EC'))
  text(bg,'PARTICLE RANGE',x+8,277,color('8999AD'))
  rect(bg,x+8,288,47,7,color(v.p1));rect(bg,x+55,288,47,7,color(v.p2))
  text(bg,v.p1,x+8,300,color('C2CDDC'));text(bg,v.p2,x+66,300,color('C2CDDC'))
  text(bg,'LIGHT FLOOR / 1X',x+8,319,color('8999AD'))
end
text(bg,'50X49 RGBA / ORIGINAL STONE + ALPHA PRESERVED / ASEPRITE / 24 FRAMES',20,397,color('7F8EA3'))
local boardLayers={}
for vi,v in ipairs(variants) do boardLayers[vi]=addLayer(board,v.name..' - preview') end
for f=1,24 do
  if f>1 then board:newEmptyFrame() end
  board.frames[f].duration=.1
  board:newCel(board.layers[1],f,bg,Point(0,0))
  for vi,v in ipairs(variants) do
    local panel=Image(120,274)
    panel:drawImage(scaled(preview[vi][f],2),Point(0,0))
    local light=floorImage(110,54,true)
    local portal=Image{fromFile=out..'/'..v.id..'-portal.png'}
    light:drawImage(portal,Point(30,2))
    panel:drawImage(light,Point(5,220))
    board:newCel(boardLayers[vi],f,panel,Point(20+(vi-1)*130,106))
  end
end
board:newTag(1,24).name='Palette comparison - synchronized particles'
board:saveAs(out..'/boss-portal-comparison.aseprite')
local still=Image(BW,BH);still:drawSprite(board,7)
scaled(still,2):saveAs(out..'/boss-portal-comparison-2x.png')
board:saveCopyAs(out..'/boss-portal-comparison.gif')
local info={source='Assets/_Project/Art/Sprites/Map/Environment/Level/Portal.png',width=W,height=H,
  membraneAlpha=129,membranePixels=maskCount,energyRimPixels=rimCount,frames=24,frameDurationMs=100,
  note='Color study only; Unity particle movement and lighting are not reproduced.',variants=variants}
local file=io.open(out..'/palette-spec.json','w');file:write(json.encode(info));file:close()
print('Aseprite '..tostring(app.version)..': authored five layered portals, five animated previews and comparison board.')
print('Source '..W..'x'..H..'; membrane '..maskCount..' pixels at alpha 129; rim '..rimCount..' pixels.')
