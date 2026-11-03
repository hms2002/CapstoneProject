local out=app.params.outputDir
local pc=app.pixelColor
local colors={pc.rgba(182,61,40,255),pc.rgba(241,107,50,255),pc.rgba(255,198,90,255),pc.rgba(255,240,191,255)}
local N,C=128,63.5
local patterns={
  -- Single gold strokes with one cream junction; no vertical color ramp.
  {'0003000','0034300','0303030','0003000','0003000','0003000','0003000'},
  {'0303030','0334330','0003000','0003000','0003000','0003000','0003000'},
  {'0033300','0030300','0034300','0003000','0033300','0003000','0003000'},
  {'0003000','0034300','0303030','0003000','0033300','0003000','0003000'},
  {'0033300','0003300','0003400','0033300','0003000','0003000','0003000'},
  {'0300030','0334330','0003000','0033300','0030300','0003000','0003000'},
  {'0003000','0034300','0003000','0033300','0300030','0003000','0003000'},
  {'0033300','0303030','0003000','0034300','0003000','0003000','0003000'},
  {'33333','30003','30003','30003','30003'},
  {'30003','30003','33433','00300','00300'},
  {'03330','30003','30403','30003','33033'},
  {'30003','30003','33433','30003','30003'},
  {'00000','03000','03430','03000','00000'},
  {'00000','03330','00400','00300','00000'},
  {'00000','00300','03430','00300','00000'},
  {'00000','03330','03030','00300','00000'}
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
local function dot(im,x,y,core)
  put(im,x-1,y,2);put(im,x+1,y,2);put(im,x,y-1,2);put(im,x,y+1,2);put(im,x,y,core or 3)
end
local function glyph(im,id,cx,cy,angle)
  local p=patterns[id];local w,h=#p[1],#p
  cx=math.floor(cx+.5);cy=math.floor(cy+.5);angle=angle or 0
  local co,si=math.cos(angle),math.sin(angle)
  if math.abs(co)<1e-10 then co=0 end;if math.abs(si)<1e-10 then si=0 end
  local coverage={}
  local function project(x,y)
    local dx,dy=x-(w+1)/2,y-(h+1)/2
    return math.floor(cx+dx*co-dy*si+.5),math.floor(cy+dx*si+dy*co+.5)
  end
  local function add(x,y,v)
    if x>=0 and y>=0 and x<im.width and y<im.height then
      local key=y*im.width+x;coverage[key]=math.max(coverage[key] or 0,v)
    end
  end
  for y=1,h do for x=1,w do
    local value=tonumber(p[y]:sub(x,x))
    if value>0 then
      local px,py=project(x,y);add(px,py,value)
      for _,offset in ipairs({{1,0},{0,1},{1,1},{1,-1}}) do
        local nx,ny=x+offset[1],y+offset[2]
        if nx<=w and ny>=1 and ny<=h then
          local other=tonumber(p[ny]:sub(nx,nx))
          if other>0 then
            local qx,qy=project(nx,ny);local steps=math.max(math.abs(qx-px),math.abs(qy-py))
            for k=1,steps-1 do add(math.floor(px+(qx-px)*k/steps+.5),math.floor(py+(qy-py)*k/steps+.5),math.max(value,other)) end
          end
        end
      end
    end
  end end
  for key,value in pairs(coverage) do im:drawPixel(key%im.width,math.floor(key/im.width),colors[value]) end
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
local atlasSource=Sprite(144,9,ColorMode.RGB);atlasSource.layers[1].name='Crimson radial sigils - enlarged medallion glyphs';atlasSource.cels[1].image=atlas
atlasSource:saveAs(out..'/glyph-atlas.aseprite');atlasSource:close()
local medallion=image(9);ring(medallion,4,4,4,2)
saveImage('medallion',medallion)
local final={}
for _,name in ipairs({'inner','outer'}) do
  local rails,detail,star,symbols,medals=image(N),image(N),image(N),image(N),image(N)
  if name=='outer' then
    ring(rails,C,C,63.5,2);ring(rails,C,C,60.5,1);ring(rails,C,C,49.5,2);ring(rails,C,C,47.5,1)
    spokes(detail,32,61,63,-math.pi/2,2)
    for k=0,15 do
      local a=-math.pi/2+k*math.pi/8
      glyph(symbols,k%8+1,C+55.5*math.cos(a),C+55.5*math.sin(a),a+math.pi/2)
      dot(detail,C+61.5*math.cos(a),C+61.5*math.sin(a))
    end
  else
    ring(rails,C,C,42.5,2);ring(rails,C,C,40.5,1);ring(rails,C,C,29.5,2)
    ring(rails,C,C,18.5,1);ring(rails,C,C,16.5,2);ring(rails,C,C,11.5,3)
    spokes(detail,32,41,42.5,math.pi/32,2)
    for k=0,3 do
      local a=-math.pi/2+k*math.pi/2
      glyph(symbols,13+k,C+14.5*math.cos(a),C+14.5*math.sin(a),a+math.pi/2)
    end
    for y=0,N-1 do for x=0,N-1 do
      local dx,dy=math.abs(x-C),math.abs(y-C)
      if math.max(dx,dy)==19.5 then put(star,x,y,2) end
      if dx+dy==28 then put(star,x,y,3) end
    end end
    for k=0,7 do
      local a=k*math.pi/4
      local x,y=C+27.5*math.cos(a),C+27.5*math.sin(a)
      -- Compact junctions replace the filled ring/point clusters.
      dot(star,x,y,k%2==0 and 4 or 3)
    end
    for k=0,11 do
      local a=-math.pi/2+k*math.pi/6
      local x,y=C+35*math.cos(a),C+35*math.sin(a)
      if k%3==0 then
        local sx,sy=math.floor(x+.5),math.floor(y+.5)
        medals:drawImage(medallion,Point(sx-4,sy-4))
        glyph(symbols,9+k/3,sx,sy,a+math.pi/2)
      else glyph(symbols,k%8+1,x,y,a+math.pi/2) end
    end
    saveImage('inner-star',star)
  end
  saveImage(name..'-base',rails)
  rails:drawImage(detail);saveImage(name..'-rings',rails)
  final[name]=saveSource(name,{{'Crimson ring bands - no rune separators',rails},{'Overlapping square octagram',star},{'Single-line centered 9px medallions',medals},{'Outward-oriented gold sigils',symbols}})
end
local combined=image(N);combined:drawImage(final.outer);combined:drawImage(final.inner);saveImage('combined-128',combined)
print('128x128 radial sigils; single-line 9px medallions with centered 5px glyphs; outer rune dividers removed')
