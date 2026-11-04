-- Native Aseprite authoring: exact pixels, separate frame/fill/player layers.
local output = app.params.output
local player = Image{fromFile=output .. '/player-idle.png'}
local rgba = app.pixelColor.rgba
local function c(hex)
  return rgba(tonumber(hex:sub(1,2),16),tonumber(hex:sub(3,4),16),tonumber(hex:sub(5,6),16),255)
end
local ink, rim, trough = c('171520'), c('76616A'), c('30252F')
local gold, pale = c('FFBB58'), c('FFF0BF')
local colors = {c('DD2E46'),c('F07826'),c('FF9D41')}
local highlights = {c('FF665B'),c('FFAB56'),c('FFEBAB')}
local function rect(im,x,y,w,h,col)
  for yy=y,y+h-1 do for xx=x,x+w-1 do im:drawPixel(xx,yy,col) end end
end
local function line(im,x,y,w,col) rect(im,x,y,w,1,col) end
local variants = {
  {id='01-slim',name='01 SLIM',w=24,h=4},
  {id='02-capsule',name='02 CAPSULE',w=20,h=5},
  {id='03-segment',name='03 SEGMENT',w=24,h=4},
  {id='04-wing',name='04 WING',w=26,h=5}
}
local glyphs={
  A={'010','101','111','101','101'},B={'110','101','110','101','110'},
  C={'011','100','100','100','011'},D={'110','101','101','101','110'},
  ['0']={'111','101','101','101','111'},['1']={'010','110','010','010','111'},
  ['2']={'110','001','010','100','111'},['3']={'110','001','010','001','110'},
  ['4']={'101','101','111','001','001'},['5']={'111','100','110','001','110'},
  ['6']={'011','100','110','101','010'},['7']={'111','001','010','010','010'},
  ['8']={'010','101','010','101','010'},['9']={'010','101','011','001','110'},
  X={'101','101','010','101','101'},['%']={'101','001','010','100','101'}
}
local function text(im,value,x,y,col)
  for k=1,#value do
    local g=glyphs[value:sub(k,k)]
    if g then for yy=1,5 do for xx=1,3 do
      if g[yy]:sub(xx,xx)=='1' then im:drawPixel(x+xx-1,y+yy-1,col) end
    end end end
    x=x+4
  end
end
local function gauge(kind,ratio)
  local v=variants[kind]
  local frame,fill=Image(v.w,v.h),Image(v.w,v.h)
  local state=ratio>=1 and 3 or ratio>=.5 and 2 or 1
  local border=ratio>=1 and gold or rim
  local x,fw,fy,fh=1,v.w-2,1,2
  if kind==1 then
    rect(frame,0,0,v.w,v.h,ink)
    line(frame,1,0,v.w-2,border)
    line(frame,1,3,v.w-2,c('483641'))
    rect(frame,1,1,v.w-2,2,trough)
  elseif kind==2 then
    rect(frame,1,0,v.w-2,5,ink);rect(frame,0,1,v.w,3,ink)
    line(frame,2,0,v.w-4,border);line(frame,2,4,v.w-4,c('483641'))
    x,fw,fy,fh=2,v.w-4,1,3
    rect(frame,x,fy,fw,fh,trough)
  elseif kind==3 then
    rect(frame,0,0,v.w,4,ink)
    line(frame,1,0,v.w-2,border)
    rect(frame,1,1,v.w-2,2,trough)
  else
    x,fw,fy,fh=4,18,1,2
    rect(frame,3,0,20,4,ink);rect(frame,4,1,18,2,trough)
    line(frame,4,0,18,border)
    for _,xx in ipairs({0,1,2,23,24,25}) do
      local yy=(xx==0 or xx==25) and 2 or (xx==1 or xx==24) and 1 or 0
      frame:drawPixel(xx,yy,border)
      if xx==2 or xx==23 then frame:drawPixel(xx,4,border) end
    end
  end
  local n=math.floor(fw*ratio+.5)
  if n>0 then
    rect(fill,x,fy,n,fh,colors[state]);line(fill,x,fy,n,highlights[state])
    if ratio>=1 then fill:drawPixel(x+n-1,fy,pale) end
  end
  if kind==3 then
    for _,xx in ipairs({6,12,18}) do
      rect(frame,xx,1,1,2,ink)
      rect(fill,xx,1,1,2,rgba(0,0,0,0))
    end
  end
  return frame,fill
end
local ratios={0,.1,.2,.3,.4,.5,.6,.7,.8,.9,1,1,1,1}
local sprites={}
for i,v in ipairs(variants) do
  local sp=Sprite(64,64,ColorMode.RGB)
  sp.layers[1].name='Backdrop (preview only)'
  local back=sp.layers[1]
  local actor=sp:newLayer();actor.name='Player (original 15x22)'
  local frameLayer=sp:newLayer();frameLayer.name=v.name..' - frame'
  local fillLayer=sp:newLayer();fillLayer.name='Charge fill - 0 to 100 percent'
  for f,ratio in ipairs(ratios) do
    if f>1 then sp:newEmptyFrame() end
    sp.frames[f].duration=.08
    local bg=Image(64,64);rect(bg,0,0,64,64,c('252630'))
    line(bg,5,59,54,c('353640'))
    sp:newCel(back,f,bg,Point(0,0))
    sp:newCel(actor,f,player,Point(25,35))
    local border,fill=gauge(i,ratio)
    local pos=Point(math.floor((64-v.w)/2),27)
    sp:newCel(frameLayer,f,border,pos);sp:newCel(fillLayer,f,fill,pos)
  end
  local tag=sp:newTag(1,#ratios);tag.name='Charge 0-100 + full hold'
  sp:saveAs(output..'/'..v.id..'.aseprite')
  sprites[i]=sp
end
-- Static comparison at 30%, 70%, 100%; one native board retains editable layers.
local board=Sprite(320,192,ColorMode.RGB)
board.layers[1].name='Preview background'
local bg=Image(320,192);rect(bg,0,0,320,192,c('1B1C24'))
board:newCel(board.layers[1],1,bg,Point(0,0))
for i,v in ipairs(variants) do
  local layer=board:newLayer();layer.name=v.name..' / '..v.w..'x'..v.h
  local image=Image(320,192)
  for row,ratio in ipairs({.3,.7,1}) do
    local x=(i-1)*80+8;local y=(row-1)*64
    rect(image,x,y,64,63,c('252630'))
    local title=string.char(64+i)..' '..v.w..'X'..v.h
    text(image,title,x+math.floor((64-#title*4)/2),y+5,c('DDD5D4'))
    local state=math.floor(ratio*100)..'%'
    text(image,state,x+math.floor((64-#state*4)/2),y+15,c('9F969E'))
    image:drawImage(player,Point(x+25,y+35))
    local border,fill=gauge(i,ratio)
    local pos=Point(x+math.floor((64-v.w)/2),y+27)
    image:drawImage(border,pos);image:drawImage(fill,pos)
  end
  board:newCel(layer,1,image,Point(0,0))
end
board:saveAs(output..'/charge-gauge-four-options.aseprite')
Image(board):saveAs(output..'/charge-gauge-four-options.png')
print('Wrote four layered animated Aseprite samples and a 30/70/100 comparison board.')
