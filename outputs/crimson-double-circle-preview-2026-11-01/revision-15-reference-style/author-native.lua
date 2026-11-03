local out=app.params.outputDir
local pc=app.pixelColor
local colors={pc.rgba(255,198,90,255),pc.rgba(255,240,191,255),pc.rgba(182,61,40,255)}
local patterns={
  -- Keep each glyph's recognizable structure; replace heavy faces with fine strokes.
  {'2222220','2000020','2000020','2222220','2000000','2000000','2000000'},
  {'1000001','1000001','1000001','1111111','0001000','0001000','0001000'},
  {'0001000','0001000','1111111','1000001','1000001','1000001','1000001'},
  {'2222222','0002000','0002000','0002000','0002000','0002000','0002000'},
  {'1111111','1000000','1000000','1111100','1000000','1000000','1111111'},
  {'1000001','1000001','1001001','1001001','1001001','1001001','1111111'},
  {'222','202','202'},
  {'202','222','020'},
  {'202','222','202'},
  {'200','202','222'},
  {'10000','10000','10000','10000','11111'},
  {'11111','00001','00001','00001','00001'},
  {'00001','00001','00001','00001','11111'},
  {'11111','10000','10000','10000','10000'}
}
local function image(n,h) return Image(n,h or n,ColorMode.RGB) end
local function ring(im,cx,cy,r,base)
  local outer=r+.5
  local function inside(x,y) return (x-cx)^2+(y-cy)^2<=outer*outer end
  for y=0,im.height-1 do for x=0,im.width-1 do
    local dx,dy=x-cx,y-cy
    -- Trace the continuous digital disk boundary, rather than layering beveled paths.
    if inside(x,y) and (not inside(x-1,y) or not inside(x+1,y) or not inside(x,y-1) or not inside(x,y+1)) then
      local tone=colors[base or 1]
      -- A few compact hot points; no per-edge metallic light/dark striping.
      if (base or 1)==1 and math.abs(dx)<1 and math.abs(dy)>r-1 then tone=colors[2] end
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
    ring(rails,c,c,39.5,1);ring(rails,c,c,27.5,3)
    ring(rails,c,c,16.5,3);ring(rails,c,c,9.5,1)
    for y=0,n-1 do for x=0,n-1 do
      local dx,dy=math.abs(x-c),math.abs(y-c)
      if math.max(dx,dy)==18.5 or dx+dy==27 then
        local tone=colors[1]
        if math.max(dx,dy)==18.5 then tone=colors[3] end
        if (dx==18.5 and dy==18.5) or (dx+dy==27 and math.min(dx,dy)<1) then tone=colors[2] end
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
    ring(rails,c,c,59.5,1);ring(rails,c,c,47.5,3)
    for k=0,15 do
      local a=-math.pi/2+k*math.pi/8
      glyph(symbols,k%6+1,c+53*math.cos(a),c+53*math.sin(a))
    end
  end
  saveImage(name..'-rings',rails)
  saveSource(name,n,{{'Fine luminous contours',rails},{'Octagram - fine strokes',star},{'Orbiting medallion frames',medallions},{'Upright luminous glyphs',symbols}})
  print(name..' '..n..'x'..n..' reference-style fine line artwork')
end
