local out=app.params.outputDir
local pc=app.pixelColor
local gold=pc.rgba(255,198,90,255)
local cream=pc.rgba(255,240,191,255)
local shadow=pc.rgba(182,61,40,255)
local function put(im,x,y,c)
  x=math.floor(x+.5);y=math.floor(y+.5)
  if x>=0 and y>=0 and x<im.width and y<im.height then im:drawPixel(x,y,c) end
end
local function ringPath(im,cx,cy,r,c)
  for x=.5,r/math.sqrt(2)+.5,1 do
    local y=math.floor(math.sqrt(math.max(0,r*r-x*x)))+.5
    for _,sx in ipairs({-1,1}) do for _,sy in ipairs({-1,1}) do
      put(im,cx+sx*x,cy+sy*y,c);put(im,cx+sx*y,cy+sy*x,c)
    end end
  end
end
local function ring(im,cx,cy,r,c)
  -- Recolor the ORIGINAL two-path mask. No added outline, join or ornament.
  local face=Image(im.width,im.height,ColorMode.RGB)
  local inset=Image(im.width,im.height,ColorMode.RGB)
  ringPath(face,cx,cy,r,c);ringPath(inset,cx,cy,r-1,c)
  for y=0,im.height-1 do for x=0,im.width-1 do
    local onFace=pc.rgbaA(face:getPixel(x,y))>0
    local onInset=pc.rgbaA(inset:getPixel(x,y))>0
    if onFace or onInset then
      local dx,dy=x-cx,y-cy
      local tone=c
      -- Flat face/inset contrast all around the ring, with four short glint clusters.
      -- No global top-to-bottom or angular light-to-dark progression.
      if onInset and not onFace then tone=shadow
      elseif onFace and math.abs(math.abs(dx)-math.abs(dy))<=math.max(1,r*.08) then tone=cream end
      im:drawPixel(x,y,tone)
    end
  end end
end
local function medallion(im,cx,cy,c) ring(im,cx,cy,4.5,c) end
local tall={
  -- Authored color clusters: 0 transparent, 1 gold face, 2 cream glint, 3 deep inset.
  {'2111100','2100130','1100130','1111300','1300000','1300000','1100000'},
  {'2100011','2100013','1100013','0111130','0021300','0011300','0011100'},
  {'0021100','0021300','0210130','0110130','1300013','1300013','1100011'},
  {'2211111','1113333','0021300','0021300','0011300','0011300','0011100'},
  {'2211111','1111333','0130000','0023000','0110000','2211111','1111333'},
  {'2100011','2100013','1100013','1301013','1301013','1111133','0110110'}
}
local cardinal={
  {'1111','1001','1001','1001'}, {'1001','1001','1111','0100'},
  {'1001','1001','1011','0110'}, {'1000','1001','0101','0010'}
}
local function glyph(im,pattern,cx,cy,a,c)
  local h,w=#pattern,#pattern[1]
  for y=1,h do for x=1,w do
    local cell=pattern[y]:sub(x,x)
    if cell~='0' then
    local dx=x-(w+1)/2;local dy=y-(h+1)/2
    -- Use each glyph's painted clusters; no height-based color bands.
    local tone=cell=='2' and cream or cell=='3' and shadow or c
    put(im,cx+dx*math.cos(a)-dy*math.sin(a),cy+dx*math.sin(a)+dy*math.cos(a),tone)
  end end end
end
for _,spec in ipairs({{'inner',80},{'outer',120}}) do
  local name,n=spec[1],spec[2]
  local c=(n-1)/2
  local layers={{'Border - flat face and dark inset',Image(n,n,ColorMode.RGB)},{'Octagram',Image(n,n,ColorMode.RGB)},{'Runes - authored color clusters',Image(n,n,ColorMode.RGB)},{'Cardinal symbols',Image(n,n,ColorMode.RGB)}}
  local rails,star,runes,accents=layers[1][2],layers[2][2],layers[3][2],layers[4][2]
  if name=='inner' then
    ring(rails,c,c,39.5,gold);ring(rails,c,c,27.5,gold)
    ring(rails,c,c,16.5,gold);ring(rails,c,c,9.5,gold)
    for y=0,n-1 do for x=0,n-1 do
      local dx,dy=math.abs(x-c),math.abs(y-c)
      if (math.max(dx,dy)>=17.5 and math.max(dx,dy)<=18.5) or (dx+dy>=26 and dx+dy<=27) then
        local tone=gold
        -- Flat inset beside a gold face, with small bright vertex clusters.
        if math.max(dx,dy)==17.5 or dx+dy==26 then tone=shadow end
        if (dx>=17.5 and dx<=18.5 and dy>=17.5 and dy<=18.5)
          or (dx+dy>=26 and math.min(dx,dy)<1) then tone=cream end
        put(star,x,y,tone)
      end
    end end
    for k=0,11 do
      local a=-math.pi/2+k*math.pi/6
      local gx,gy=c+33*math.cos(a),c+33*math.sin(a)
      if k%3==0 then
        medallion(rails,gx,gy,gold)
        glyph(accents,cardinal[k/3+1],gx,gy,k*math.pi/6,gold)
      else glyph(runes,tall[k%6+1],gx,gy,k*math.pi/6,gold) end
    end
    local centerMarks={{'1100','1100','1111'},{'1111','0011','0011'},{'0011','0011','1111'},{'1111','1100','1100'}}
    for k=0,3 do
      local a=-math.pi/2+k*math.pi/2
      glyph(runes,centerMarks[k+1],c+12.5*math.cos(a),c+12.5*math.sin(a),a+math.pi/2,gold)
    end
  else
    ring(rails,c,c,59.5,gold);ring(rails,c,c,47.5,gold)
    for k=0,15 do
      local a=-math.pi/2+k*math.pi/8
      glyph(runes,tall[k%6+1],c+53*math.cos(a),c+53*math.sin(a),k*math.pi/8,gold)
    end
  end
  -- Border ownership/mask is unchanged; only its existing pixels are recolored.
  for y=0,n-1 do for x=0,n-1 do
    if pc.rgbaA(rails:getPixel(x,y))>0 then
      star:drawPixel(x,y,0);runes:drawPixel(x,y,0);accents:drawPixel(x,y,0)
    end
  end end
  local s=Sprite(n,n,ColorMode.RGB)
  local composite=Image(n,n,ColorMode.RGB)
  for i,specLayer in ipairs(layers) do
    local layer=i==1 and s.layers[1] or s:newLayer()
    layer.name=specLayer[1]
    if i==1 then s.cels[1].image=specLayer[2] else s:newCel(layer,1,specLayer[2],Point(0,0)) end
    composite:drawImage(specLayer[2])
  end
  s:saveAs(out..'/'..name..'.aseprite');s:saveCopyAs(out..'/'..name..'-preview.png')
  local f=assert(io.open(out..'/'..name..'.rgba','wb'));f:write(composite.bytes);f:close()
  local borderFile=assert(io.open(out..'/'..name..'-borders.rgba','wb'));borderFile:write(rails.bytes);borderFile:close()
  s:close()
  print(name..' '..n..'x'..n..' native-grid artwork')
end
