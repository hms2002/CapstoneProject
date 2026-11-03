local out=app.params.outputDir
local pc=app.pixelColor
local function core(p,inner)
  return pc.rgbaA(p)>=220 and pc.rgbaG(p)>=220 and pc.rgbaB(p)>=(inner and 105 or 135)
end
for _,spec in ipairs({{'inner',64},{'outer',96}}) do
  local name,n=spec[1],spec[2]
  local src=Image{fromFile=out..'/'..name..'-source.png'}
  local cols,rows={},{}
  for x=0,src.width-1 do cols[x]=0 end
  for y=0,src.height-1 do rows[y]=0 end
  for y=0,src.height-1 do for x=0,src.width-1 do
    if core(src:getPixel(x,y),name=='inner') then cols[x]=cols[x]+1;rows[y]=rows[y]+1 end
  end end
  local minX,maxX,minY,maxY=src.width,0,src.height,0
  for x=0,src.width-1 do if cols[x]>=src.height*.012 then minX=math.min(minX,x);maxX=math.max(maxX,x) end end
  for y=0,src.height-1 do if rows[y]>=src.width*.012 then minY=math.min(minY,y);maxY=math.max(maxY,y) end end
  local cx,cy=(minX+maxX)/2,(minY+maxY)/2
  local side=math.max(maxX-minX+1,maxY-minY+1)
  local im=Image(n,n,ColorMode.RGB)
  local count=0
  for y=0,n-1 do for x=0,n-1 do
    local hits,total=0,0
    for sy=math.max(0,math.floor(cy+(y/n-.5)*side)),math.min(src.height-1,math.ceil(cy+((y+1)/n-.5)*side)-1) do
      for sx=math.max(0,math.floor(cx+(x/n-.5)*side)),math.min(src.width-1,math.ceil(cx+((x+1)/n-.5)*side)-1) do
        total=total+1
        if core(src:getPixel(sx,sy),name=='inner') then hits=hits+1 end
      end
    end
    if total>0 and hits/total>=.17 and (x+.5-n/2)^2+(y+.5-n/2)^2<=(n/2)^2 then
      im:drawPixel(x,y,pc.rgba(255,198,90,255));count=count+1
    end
  end end
  local s=Sprite(n,n,ColorMode.RGB)
  s.layers[1].name=name=='inner' and 'Inner - complete circle and octagram' or 'Outer - independent rune ring'
  s.cels[1].image=im
  s:saveAs(out..'/'..name..'.aseprite')
  s:saveCopyAs(out..'/'..name..'-preview.png')
  local f=assert(io.open(out..'/'..name..'.rgba','wb'));f:write(im.bytes);f:close()
  print(name..' '..n..'x'..n..' opaque='..count..' bounds='..minX..','..minY..','..maxX..','..maxY)
  s:close()
end
