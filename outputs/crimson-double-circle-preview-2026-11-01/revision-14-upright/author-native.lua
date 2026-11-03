local out=app.params.outputDir
local pc=app.pixelColor
local colors={pc.rgba(255,198,90,255),pc.rgba(255,240,191,255),pc.rgba(182,61,40,255)}
local patterns={
  {'2211110','2211330','1100130','1111130','1100000','1100000','1100000'},
  {'1100011','1100013','1100013','1111113','0011100','0011100','0011100'},
  {'0011100','0011100','1111111','1100013','1100013','1100013','1100011'},
  {'2211111','2211111','0011300','0011300','0011300','0011300','0011100'},
  {'2211111','2200000','1100000','1111300','1100000','1100000','1111111'},
  {'1100011','1100013','1101013','1101013','1101013','1111113','1111111'},
  {'111','101','101'},
  {'101','111','010'},
  {'101','111','101'},
  {'100','101','111'},
  {'11000','11000','11000','11111','11111'},
  {'11111','11111','00011','00011','00011'},
  {'00011','00011','00011','11111','11111'},
  {'11111','11111','11000','11000','11000'}
}
local function image(n,h) return Image(n,h or n,ColorMode.RGB) end
local function ring(im,cx,cy,r)
  local outer=r+.5
  local inner=outer-2
  for y=0,im.height-1 do for x=0,im.width-1 do
    local dx,dy=x-cx,y-cy
    local d=math.sqrt(dx*dx+dy*dy)
    if d<=outer and d>=inner then
      local tone=colors[1]
      if d<inner+.65 then tone=colors[3]
      elseif math.abs(math.abs(dx)-math.abs(dy))<=math.max(1,r*.06) then tone=colors[2] end
      im:drawPixel(x,y,tone)
    end
  end end
end
local function glyph(im,id,cx,cy)
  local p=patterns[id]
  local left=math.floor(cx-(#p[1]-1)/2+.5)
  local top=math.floor(cy-(#p-1)/2+.5)
  for y=1,#p do for x=1,#p[y] do
    local v=tonumber(p[y]:sub(x,x))
    if v>0 then im:drawPixel(left+x-1,top+y-1,colors[v]) end
  end end
end
local function raw(name,im)
  local f=assert(io.open(out..'/'..name..'.rgba','wb'));f:write(im.bytes);f:close()
end
local function saveImage(name,im)
  local s=Sprite(im.width,im.height,ColorMode.RGB);s.cels[1].image=im
  s:saveCopyAs(out..'/'..name..'.png');s:close();raw(name,im)
end
local function saveSource(name,n,layers)
  local s=Sprite(n,n,ColorMode.RGB);local composite=image(n)
  for i,entry in ipairs(layers) do
    local layer=i==1 and s.layers[1] or s:newLayer();layer.name=entry[1]
    if i==1 then s.cels[1].image=entry[2] else s:newCel(layer,1,entry[2],Point(0,0)) end
    composite:drawImage(entry[2])
  end
  s:saveAs(out..'/'..name..'.aseprite');s:saveCopyAs(out..'/'..name..'-preview.png');s:close()
  raw(name,composite)
end
local atlas=image(126,9)
for id=1,#patterns do glyph(atlas,id,(id-1)*9+4,4) end
saveImage('glyph-atlas',atlas)
local atlasSource=Sprite(126,9,ColorMode.RGB);atlasSource.layers[1].name='Upright glyphs - native pixel clusters';atlasSource.cels[1].image=atlas
atlasSource:saveAs(out..'/glyph-atlas.aseprite');atlasSource:close()
local medallion=image(10);ring(medallion,4.5,4.5,4.5);saveImage('medallion',medallion)
for _,entry in ipairs({{'inner',80},{'outer',120}}) do
  local name,n=entry[1],entry[2];local c=(n-1)/2
  local rails,star,symbols,medallions=image(n),image(n),image(n),image(n)
  if name=='inner' then
    for _,r in ipairs({39.5,27.5,16.5,9.5}) do ring(rails,c,c,r) end
    for y=0,n-1 do for x=0,n-1 do
      local dx,dy=math.abs(x-c),math.abs(y-c)
      if (math.max(dx,dy)>=17.5 and math.max(dx,dy)<=18.5) or (dx+dy>=26 and dx+dy<=27) then
        local tone=colors[1]
        if math.max(dx,dy)==17.5 or dx+dy==26 then tone=colors[3] end
        if (dx>=17.5 and dx<=18.5 and dy>=17.5 and dy<=18.5) or (dx+dy>=26 and math.min(dx,dy)<1) then tone=colors[2] end
        star:drawPixel(x,y,tone)
      end
    end end
    for k=0,11 do
      local a=-math.pi/2+k*math.pi/6
      local gx,gy=c+33*math.cos(a),c+33*math.sin(a)
      if k%3==0 then
        medallions:drawImage(medallion,Point(math.floor(gx-4.5+.5),math.floor(gy-4.5+.5)))
        glyph(symbols,7+k/3,gx,gy)
      else glyph(symbols,k%6+1,gx,gy) end
    end
    for k=0,3 do
      local a=-math.pi/2+k*math.pi/2
      glyph(symbols,11+k,c+12.5*math.cos(a),c+12.5*math.sin(a))
    end
    -- Keep ring fronts readable at octagram contact points.
    for y=0,n-1 do for x=0,n-1 do if pc.rgbaA(rails:getPixel(x,y))>0 then star:drawPixel(x,y,0) end end end
    saveImage('inner-star',star)
  else
    ring(rails,c,c,59.5);ring(rails,c,c,47.5)
    for k=0,15 do
      local a=-math.pi/2+k*math.pi/8
      glyph(symbols,k%6+1,c+53*math.cos(a),c+53*math.sin(a))
    end
  end
  saveImage(name..'-rings',rails)
  saveSource(name,n,{{'Continuous 2px annular borders',rails},{'Octagram',star},{'Orbiting medallion frames',medallions},{'Upright glyphs',symbols}})
  print(name..' '..n..'x'..n..' repaired rings and upright glyphs')
end
