-- Native pixel authoring executed inside Aseprite, without raster generation.
local out=assert(app.params.outputDir)
local pc=app.pixelColor
local N=64
local function rgba(hex)
  return pc.rgba(tonumber(hex:sub(1,2),16),tonumber(hex:sub(3,4),16),tonumber(hex:sub(5,6),16),255)
end
local function put(im,x,y,c)
  x=math.floor(x+.5);y=math.floor(y+.5)
  if x>=0 and y>=0 and x<im.width and y<im.height then im:drawPixel(x,y,c) end
end
local function poly(im,points,c)
  for y=0,im.height-1 do
    local xs={}
    for k,a in ipairs(points) do
      local b=points[k%#points+1]
      if (a[2]<=y and b[2]>y) or (b[2]<=y and a[2]>y) then
        xs[#xs+1]=a[1]+(y-a[2])*(b[1]-a[1])/(b[2]-a[2])
      end
    end
    table.sort(xs)
    for k=1,#xs-1,2 do for x=math.ceil(xs[k]),math.floor(xs[k+1]) do put(im,x,y,c) end end
  end
end
local function line(im,x1,y1,x2,y2,c)
  local dx,dy=x2-x1,y2-y1
  local steps=math.max(math.abs(dx),math.abs(dy))
  for t=0,steps do local u=steps==0 and 0 or t/steps;put(im,x1+dx*u,y1+dy*u,c) end
end
local function arc(im,cx,cy,rx,ry,a,b,c)
  for d=a,b,4 do local t=d*math.pi/180;put(im,cx+rx*math.cos(t),cy+ry*math.sin(t),c) end
end
local function clip(im,mask)
  for y=0,N-1 do for x=0,N-1 do if mask:getPixel(x,y)==0 then im:drawPixel(x,y,0) end end end
end
local outline={{29,8},{35,8},{39,11},{44,11},{48,15},{48,20},{53,23},{55,28},{52,32},{54,37},{52,42},{47,44},{46,49},{41,52},{36,51},{32,55},{26,54},{22,51},{17,51},{12,47},{13,42},{8,39},{7,33},{10,28},{9,23},{12,18},{18,17},{20,12},{25,12}}
local mask=Image(N,N,ColorMode.RGB)
poly(mask,outline,rgba('FFFFFF'))
local function flatBody(edge,base)
  local im=Image(N,N,ColorMode.RGB)
  for y=1,N-2 do for x=1,N-2 do
    if mask:getPixel(x,y)~=0 then
      local border=mask:getPixel(x-1,y)==0 or mask:getPixel(x+1,y)==0 or mask:getPixel(x,y-1)==0 or mask:getPixel(x,y+1)==0
      put(im,x,y,border and edge or base)
    end
  end end
  return im
end
local function wetContour(im,dark,light)
  -- A one-pixel surface boundary, never an extruded side wall.
  for y=1,N-2 do for x=1,N-2 do
    if mask:getPixel(x,y)~=0 then
      local border=mask:getPixel(x-1,y)==0 or mask:getPixel(x+1,y)==0 or mask:getPixel(x,y-1)==0 or mask:getPixel(x,y+1)==0
      if border then
        if (x+y)%17<11 then put(im,x,y,dark)
        elseif x+y<60 then put(im,x,y,light) end
      end
    end
  end end
end
local function bubble(im,x,y,stage,c1,c2)
  if stage==0 then put(im,x,y,c1);put(im,x+1,y,c2)
  elseif stage==1 then
    line(im,x,y-1,x+1,y-1,c2);put(im,x-1,y,c2);put(im,x+2,y,c1);line(im,x,y+1,x+1,y+1,c1)
  elseif stage==2 then
    line(im,x-1,y-2,x+1,y-2,c2);line(im,x-2,y-1,x-2,y+1,c2);line(im,x+2,y-1,x+2,y+1,c1);line(im,x-1,y+2,x+1,y+2,c1)
  else
    put(im,x-3,y,c1);put(im,x+3,y,c1);line(im,x-1,y+2,x+1,y+2,c1)
  end
end
local function alcohol(f)
  local c={rgba('965E2F'),rgba('B47835'),rgba('C58C41'),rgba('D9A655'),rgba('E5C278'),rgba('F5E5AF'),rgba('68412D'),rgba('FFF4D0')}
  local body=flatBody(c[1],c[2])
  -- Flat color islands, without a front bevel or cast shadow.
  poly(body,{{16,19},{24,16},{34,16},{34,19},{29,20},{23,19},{19,23},{13,25}},c[3])
  poly(body,{{37,28},{46,25},{50,29},{46,31},{45,36},{38,38},{34,35},{37,33}},c[3])
  poly(body,{{17,39},{23,38},{26,41},{34,43},{39,41},{43,44},{38,48},{30,48},{28,45},{22,45},{18,43}},c[3])
  -- Quiet dark pools and narrow reflected edges make the material read as liquid.
  poly(body,{{12,29},{17,27},{23,28},{21,30},{16,30},{13,33}},c[1])
  poly(body,{{32,18},{38,19},{42,23},{39,24},{36,21},{32,21}},c[1])
  poly(body,{{31,37},{34,37},{36,40},{42,39},{43,41},{37,43},{32,42}},c[1])
  line(body,14,30,18,29,c[7]);line(body,37,41,41,40,c[7])
  line(body,21,16,25,15,c[4]);line(body,29,11,34,11,c[4])
  wetContour(body,c[7],c[4])
  clip(body,mask)
  local ripples=Image(N,N,ColorMode.RGB)
  local shift=({0,1,0,-1})[f+1]
  arc(ripples,26,29,11,9,206+f*3,255+f*3,c[3])
  arc(ripples,31,33,15,13,23+shift*3,68+shift*3,c[3])
  line(ripples,19+shift,25,23+shift,25,c[4])
  line(ripples,21+shift,24,24+shift,24,c[3])
  line(ripples,36-shift,43,40-shift,43,c[4])
  line(ripples,36-shift,44,38-shift,44,c[3])
  line(ripples,18+shift,26,22+shift,26,c[1])
  line(ripples,19+shift,25,22+shift,25,c[5])
  line(ripples,20+shift,24,21+shift,24,c[8])
  arc(ripples,30,32,10,8,290+shift*4,330+shift*4,c[4])
  arc(ripples,30,32,10,8,294+shift*4,309+shift*4,c[5])
  line(ripples,36-shift,42,38-shift,42,c[6])
  local foam=Image(N,N,ColorMode.RGB)
  -- Small foam islands sit on the surface rather than above it.
  for _,p in ipairs({{16,22},{42,16},{47,39},{21,46}}) do
    local x,y=p[1],p[2]
    line(foam,x-1,y+1,x+3,y+1,c[1]);put(foam,x+4,y,c[7])
    line(foam,x,y,x+3,y,c[5]);line(foam,x+1,y-1,x+3,y-1,c[6])
    put(foam,x,y-1,c[6]);put(foam,x+2,y-2,c[6]);put(foam,x+1,y-1,c[8])
    put(foam,x+5,y+1,c[5]);put(foam,x+5,y,c[6])
  end
  bubble(foam,25,33,f,c[4],c[6])
  bubble(foam,39,28,(f+2)%4,c[4],c[5])
  if f==1 then put(foam,25,32,c[8]) elseif f==2 then put(foam,24,31,c[8]) end
  if (f+2)%4==2 then put(foam,38,26,c[8]) end
  clip(ripples,mask);clip(foam,mask)
  return {body,ripples,foam},c
end
local flamePatterns={
  {'....2..','...22..','...23..','..233..','.2332..','.23432.','223432.','.23322.','..222..'},
  {'.....2.','....22.','...232.','...32..','..233..','.2332..','223432.','.234322','..222..'},
  {'...2...','...2...','..22.2.','..23.2.','..2332.','.23332.','223432.','.234322','..222..'},
  {'.......','...2...','..22...','..232..','.2332..','.23432.','223432.','.23322.','..222..'}
}
local function fire(f)
  -- The previous sixth swatch was unused. Preserve five actual colors and add two.
  local c={rgba('AD452F'),rgba('D15A30'),rgba('E77835'),rgba('FFA941'),rgba('FFE49A'),rgba('77343A'),rgba('FFF5D2')}
  local body=flatBody(c[1],c[2])
  poly(body,{{17,16},{28,13},{38,16},{43,16},{44,20},{37,22},{29,19},{22,21},{16,24},{12,24}},c[3])
  poly(body,{{17,32},{25,30},{31,32},{36,28},{46,29},{48,33},{43,36},{37,34},{32,38},{25,36},{20,38},{16,36}},c[3])
  poly(body,{{16,43},{23,42},{30,46},{35,43},{43,44},{42,48},{36,49},{31,51},{23,47},{18,48}},c[3])
  -- Dark gaps between low tongues establish contrast without a raised platform.
  poly(body,{{18,25},{23,23},{28,24},{30,27},{28,29},{26,27},{22,27},{20,29},{17,28}},c[1])
  poly(body,{{34,28},{39,27},{43,29},{42,32},{39,32},{37,30},{34,31}},c[1])
  poly(body,{{27,39},{32,37},{35,39},{34,42},{31,41},{29,43},{26,42}},c[1])
  line(body,20,27,23,26,c[6]);line(body,27,26,28,27,c[6])
  line(body,38,29,40,30,c[6]);line(body,29,41,31,40,c[6])
  wetContour(body,c[6],c[3])
  clip(body,mask)
  local heat=Image(N,N,ColorMode.RGB)
  local sh=({0,1,0,-1})[f+1]
  line(heat,22+sh,24,26+sh,24,c[4]);put(heat,27+sh,23,c[4])
  line(heat,37-sh,39,41-sh,39,c[4]);put(heat,36-sh,40,c[4])
  line(heat,23,47-sh,26,47-sh,c[4])
  local flames=Image(N,N,ColorMode.RGB)
  local anchors={{24,20,0},{39,23,2},{16,34,1},{31,34,3},{46,37,0},{25,46,2},{39,47,1}}
  for _,a in ipairs(anchors) do
    local stage=(f+a[3])%4
    local pat=flamePatterns[stage+1]
    -- Short dark-red edge on the flame itself; same native height as revision 2.
    for y,row in ipairs(pat) do for x=1,#row do
      if tonumber(row:sub(x,x)) then
        local px,py=a[1]+x-4,a[2]+y-9
        put(flames,px-1,py,c[1]);put(flames,px+1,py,c[1]);put(flames,px,py+1,c[1])
      end
    end end
    for y,row in ipairs(pat) do for x=1,#row do
      local k=tonumber(row:sub(x,x))
      if k then
        local color=k==4 and c[7] or c[k+1]
        put(flames,a[1]+x-4,a[2]+y-9,color)
        if k==4 then put(flames,a[1]+x-5,a[2]+y-9,c[5]) end
      end
    end end
    line(flames,a[1]-1,a[2]+1,a[1]+2,a[2]+1,c[6])
  end
  clip(heat,mask);clip(flames,mask)
  return {body,heat,flames},c
end
local specs={{'alcohol',alcohol,.16,{'01 Ground - fixed flat footprint','02 Surface ripples','03 Foam and bubbles'}},{'fire',fire,.10,{'01 Ground - fixed flat footprint','02 Surface heat','03 Low flames'}}}
local made={}
for _,spec in ipairs(specs) do
  local name,draw,duration,names=table.unpack(spec)
  local s=Sprite(N,N,ColorMode.RGB)
  s.gridBounds=Rectangle(0,0,1,1)
  local layers={s.layers[1],s:newLayer(),s:newLayer()}
  for i,l in ipairs(layers) do l.name=names[i] end
  local _,colors=draw(0)
  local palette=Palette(#colors+1);palette:setColor(0,Color{r=0,g=0,b=0,a=0})
  for i,c in ipairs(colors) do palette:setColor(i,c) end
  s:setPalette(palette)
  for f=0,3 do
    if f>0 then s:newEmptyFrame() end
    s.frames[f+1].duration=duration
    local ims=draw(f)
    for i,im in ipairs(ims) do
      local old=layers[i]:cel(f+1)
      if old then s:deleteCel(old) end
      s:newCel(layers[i],f+1,im,Point(0,0))
    end
    local flat=Image(N,N,ColorMode.RGB);flat:drawSprite(s,f+1)
    flat:saveAs(out..'/'..name..'-frame-'..(f+1)..'.png')
  end
  local tag=s:newTag(1,4);tag.name='Idle'
  local slice=s:newSlice(Rectangle(0,0,N,N));slice.name='GroundCenter';slice.pivot=Point(32,32)
  s.data='Native 64x64 flat-ground sample; fixed pivot (32,32); no Unity integration. Authored in Aseprite via Lua.'
  s:saveAs(out..'/'..name..'.aseprite')
  s:saveCopyAs(out..'/'..name..'.gif')
  local sheet=Image(N*4,N,ColorMode.RGB)
  for f=1,4 do sheet:drawSprite(s,f,Point((f-1)*N,0)) end
  sheet:saveAs(out..'/'..name..'-sheet.png')
  made[#made+1]=s
end
-- Two side-by-side native sprites on neutral flat preview tiles, 4x enlargement.
local preview=Sprite(152,80,ColorMode.RGB)
preview.layers[1].name='Review only - neutral ground'
-- 3.2s is the shared repeat period of 640ms alcohol and 400ms fire loops.
for f=1,160 do
  if f>1 then preview:newEmptyFrame() end
  preview.frames[f].duration=.02
  local im=Image(152,80,ColorMode.RGB)
  for y=0,79 do for x=0,151 do
    local c=(x%16==0 or y%16==0) and rgba('242B30') or rgba('2B3339')
    if x>=76 then c=(x%16==0 or y%16==0) and rgba('384047') or rgba('414A50') end
    put(im,x,y,c)
  end end
  local alcoholFlat=Image(N,N,ColorMode.RGB)
  local fireFlat=Image(N,N,ColorMode.RGB)
  alcoholFlat:drawSprite(made[1],math.floor((f-1)/8)%4+1)
  fireFlat:drawSprite(made[2],math.floor((f-1)/5)%4+1)
  im:drawImage(alcoholFlat,Point(6,8));im:drawImage(fireFlat,Point(82,8))
  local cel=preview.layers[1]:cel(f)
  if cel then preview:deleteCel(cel) end
  preview:newCel(preview.layers[1],f,im,Point(0,0))
end
preview:resize(608,320)
preview:saveAs(out..'/comparison-preview.aseprite')
preview:saveCopyAs(out..'/comparison-preview.gif')
local still=Image(preview);still:saveAs(out..'/comparison-preview.png')
preview:close()
for _,s in ipairs(made) do s:close() end
local done=assert(io.open(out..'/authoring-complete.txt','w'))
done:write('Aseprite '..tostring(app.version)..'\n2 sources; 64x64; 4 frames; 3 layers; binary alpha; fixed ground pivot (32,32).\n')
done:close()
