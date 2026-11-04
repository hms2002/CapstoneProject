local out=app.params.outputDir
local pc=app.pixelColor
local colors={pc.rgba(62,34,110,255),pc.rgba(128,82,216,255),pc.rgba(188,160,250,255),pc.rgba(239,227,255,255)}
local N,C=128,63.5
local patterns={
  {'0004000','0044400','0433340','0003000','0003000','0003000','0002000'},
  {'0404040','0433340','0003000','0003000','0003000','0002000','0002000'},
  {'0044400','0040400','0043400','0003000','0033300','0002000','0002000'},
  {'0004000','0043400','0403040','0003000','0044400','0002000','0002000'},
  {'0044400','0003400','0003400','0044400','0003000','0003000','0002000'},
  {'0400040','0433340','0003000','0033300','0030300','0002000','0002000'},
  {'0004000','0044400','0003000','0033300','0300030','0002000','0002000'},
  {'0044400','0403040','0003000','0033300','0003000','0003000','0002000'},
  {'444','404','404'}, {'404','444','040'}, {'404','444','404'}, {'400','404','444'},
  {'00400','03330','00300','00300','00200'},
  {'04440','00300','00300','03330','00200'},
  {'00400','00300','03330','00300','00200'},
  {'04440','04040','00300','00300','00200'}
}
local function image(w,h) return Image(w,h or w,ColorMode.RGB) end
local function put(im,x,y,color)
  x=math.floor(x+.5);y=math.floor(y+.5)
  if x>=0 and y>=0 and x<im.width and y<im.height then im:drawPixel(x,y,colors[color]) end
end
local function line(im,x0,y0,x1,y1,color)
  x0=math.floor(x0+.5);y0=math.floor(y0+.5);x1=math.floor(x1+.5);y1=math.floor(y1+.5)
  local dx=math.abs(x1-x0);local sx=x0<x1 and 1 or -1
  local dy=-math.abs(y1-y0);local sy=y0<y1 and 1 or -1;local err=dx+dy
  while true do
    put(im,x0,y0,color);if x0==x1 and y0==y1 then break end
    local twice=2*err
    if twice>=dy then err=err+dy;x0=x0+sx end
    if twice<=dx then err=err+dx;y0=y0+sy end
  end
end
local function ring(im,cx,cy,r,color)
  local outer=r+.5
  local proof=(cx==C and cy==C and r>3) and image(N) or nil
  local function inside(x,y) return (x-cx)^2+(y-cy)^2<=outer*outer end
  for y=math.max(0,math.floor(cy-outer)),math.min(im.height-1,math.ceil(cy+outer)) do
    for x=math.max(0,math.floor(cx-outer)),math.min(im.width-1,math.ceil(cx+outer)) do
      if inside(x,y) and (not inside(x-1,y) or not inside(x+1,y) or not inside(x,y-1) or not inside(x,y+1)) then
        put(im,x,y,color);if proof then put(proof,x,y,color) end
      end
    end
  end
  if proof then local f=assert(io.open(out..'/ring-mask-'..r..'.rgba','wb'));f:write(proof.bytes);f:close() end
end
local function spokes(im,count,r1,r2,offset,color)
  for k=0,count-1 do
    local a=offset+k*math.pi*2/count
    line(im,C+r1*math.cos(a),C+r1*math.sin(a),C+r2*math.cos(a),C+r2*math.sin(a),color)
  end
end
local function dot(im,x,y)
  put(im,x-1,y,2);put(im,x+1,y,2);put(im,x,y-1,2);put(im,x,y+1,2);put(im,x,y,4)
end
local function glyph(im,id,cx,cy)
  local p=patterns[id];local left=math.floor(cx-(#p[1]-1)/2+.5);local top=math.floor(cy-(#p-1)/2+.5)
  for y=1,#p do for x=1,#p[y] do
    local value=tonumber(p[y]:sub(x,x));if value>0 then put(im,left+x-1,top+y-1,value) end
  end end
end
local function raw(name,im)
  local f=assert(io.open(out..'/'..name..'.rgba','wb'));f:write(im.bytes);f:close()
end
local function saveImage(name,im)
  local s=Sprite(im.width,im.height,ColorMode.RGB);s.cels[1].image=im
  s:saveCopyAs(out..'/'..name..'.png');s:close();raw(name,im)
end
local function saveSource(name,layers)
  local s=Sprite(N,N,ColorMode.RGB);local composite=image(N)
  for i,entry in ipairs(layers) do
    local layer=i==1 and s.layers[1] or s:newLayer();layer.name=entry[1]
    if i==1 then s.cels[1].image=entry[2] else s:newCel(layer,1,entry[2],Point(0,0)) end
    composite:drawImage(entry[2])
  end
  s:saveAs(out..'/'..name..'.aseprite');s:saveCopyAs(out..'/'..name..'-preview.png');s:close();raw(name,composite)
  return composite
end
local atlas=image(144,9)
for id=1,#patterns do glyph(atlas,id,(id-1)*9+4,4) end
saveImage('glyph-atlas',atlas)
local atlasSource=Sprite(144,9,ColorMode.RGB);atlasSource.layers[1].name='Arcane Lattice upright sigils';atlasSource.cels[1].image=atlas
atlasSource:saveAs(out..'/glyph-atlas.aseprite');atlasSource:close()
local medallion=image(10);ring(medallion,4.5,4.5,4.5,2);ring(medallion,4.5,4.5,3.5,3)
saveImage('medallion',medallion)
local final={}
for _,name in ipairs({'inner','outer'}) do
  local rails,detail,star,symbols,medals=image(N),image(N),image(N),image(N),image(N)
  if name=='outer' then
    ring(rails,C,C,63.5,2);ring(rails,C,C,60.5,1);ring(rails,C,C,49.5,2);ring(rails,C,C,47.5,1)
    spokes(detail,48,61,63,-math.pi/2,2)
    spokes(detail,16,48,49.5,-math.pi/2+math.pi/16,3)
    for k=0,15 do
      local a=-math.pi/2+k*math.pi/8
      glyph(symbols,k%8+1,C+55.5*math.cos(a),C+55.5*math.sin(a))
      local b=a+math.pi/16
      -- Small etched separators in the broad rune band, rather than large letters.
      line(detail,C+53*math.cos(b),C+53*math.sin(b),C+57*math.cos(b),C+57*math.sin(b),1)
      dot(detail,C+61.5*math.cos(a),C+61.5*math.sin(a))
    end
  else
    ring(rails,C,C,42.5,2);ring(rails,C,C,40.5,1);ring(rails,C,C,29.5,2)
    ring(rails,C,C,18.5,1);ring(rails,C,C,16.5,2);ring(rails,C,C,11.5,3)
    spokes(detail,32,41,42.5,math.pi/32,2)
    for k=0,3 do
      local a=-math.pi/2+k*math.pi/2
      glyph(symbols,13+k,C+14.5*math.cos(a),C+14.5*math.sin(a))
    end
    for y=0,N-1 do for x=0,N-1 do
      local dx,dy=math.abs(x-C),math.abs(y-C)
      if math.max(dx,dy)==19.5 then put(star,x,y,2) end
      if dx+dy==28 then put(star,x,y,3) end
    end end
    for k=0,7 do
      local a=k*math.pi/4
      local x,y=C+27.5*math.cos(a),C+27.5*math.sin(a)
      ring(star,x,y,1.5,2);dot(star,x,y)
    end
    for k=0,11 do
      local a=-math.pi/2+k*math.pi/6
      local x,y=C+35*math.cos(a),C+35*math.sin(a)
      if k%3==0 then
        medals:drawImage(medallion,Point(math.floor(x-4.5+.5),math.floor(y-4.5+.5)))
        glyph(symbols,9+k/3,x,y)
      else glyph(symbols,k%8+1,x,y) end
    end
    saveImage('inner-star',star)
  end
  saveImage(name..'-base',rails)
  rails:drawImage(detail);saveImage(name..'-rings',rails)
  final[name]=saveSource(name,{{'Violet engraved ring bands',rails},{'Overlapping square octagram',star},{'Four medallion frames',medals},{'Small luminous arcane runes',symbols}})
end
local combined=image(N);combined:drawImage(final.outer);combined:drawImage(final.inner);saveImage('combined-128',combined)
print('128x128 inner/outer sources; inner visible diameter 86px; outer 128px; Arcane Lattice palette and sigils')
