-- Aseprite native art: bright beer, planar combustion, ignition, independent particles.
local out=assert(app.params.outputDir)
local pc=app.pixelColor
local N=64
local function rgba(s) return pc.rgba(tonumber(s:sub(1,2),16),tonumber(s:sub(3,4),16),tonumber(s:sub(5,6),16),255) end
local function colors(list) local c={};for _,s in ipairs(list) do c[#c+1]=rgba(s) end;return c end
local B=colors({'BE7B21','E8AD32','F4C74B','FFDB6B','D99526','E6D39B','FFF0CB','FFFCED'})
local F=colors({'AC3E2D','DF5926','F68A2F','FFB83E','FFE583','7D352D','FFF7CB'})
local function im(w,h) return Image(w or N,h or N,ColorMode.RGB) end
local function put(dst,x,y,c)
  x=math.floor(x+.5);y=math.floor(y+.5)
  if x>=0 and y>=0 and x<dst.width and y<dst.height then dst:drawPixel(x,y,c) end
end
local function poly(dst,pts,c)
  for y=0,dst.height-1 do
    local xs={}
    for i,a in ipairs(pts) do local b=pts[i%#pts+1]
      if (a[2]<=y and b[2]>y) or (b[2]<=y and a[2]>y) then xs[#xs+1]=a[1]+(y-a[2])*(b[1]-a[1])/(b[2]-a[2]) end
    end
    table.sort(xs)
    for k=1,#xs-1,2 do for x=math.ceil(xs[k]),math.floor(xs[k+1]) do put(dst,x,y,c) end end
  end
end
local function line(dst,x1,y1,x2,y2,c)
  local dx,dy=x2-x1,y2-y1;local steps=math.max(math.abs(dx),math.abs(dy))
  for i=0,steps do local t=steps==0 and 0 or i/steps;put(dst,x1+t*dx,y1+t*dy,c) end
end
local function ellipse(dst,cx,cy,rx,ry,c)
  for y=math.floor(cy-ry),math.ceil(cy+ry) do for x=math.floor(cx-rx),math.ceil(cx+rx) do
    if ((x-cx)/rx)^2+((y-cy)/ry)^2<=1 then put(dst,x,y,c) end
  end end
end
local function arc(dst,cx,cy,rx,ry,a,b,c)
  for d=a,b,3 do local t=d*math.pi/180;put(dst,cx+rx*math.cos(t),cy+ry*math.sin(t),c) end
end
local outline={{29,8},{35,8},{39,11},{44,11},{48,15},{48,20},{53,23},{55,28},{52,32},{54,37},{52,42},{47,44},{46,49},{41,52},{36,51},{32,55},{26,54},{22,51},{17,51},{12,47},{13,42},{8,39},{7,33},{10,28},{9,23},{12,18},{18,17},{20,12},{25,12}}
local mask=im();poly(mask,outline,rgba('FFFFFF'))
local function inside(x,y) return x>=0 and y>=0 and x<N and y<N and mask:getPixel(x,y)~=0 end
local function clip(dst)
  for y=0,N-1 do for x=0,N-1 do if not inside(x,y) then dst:drawPixel(x,y,0) end end end
end
local function edge(x,y) return inside(x,y) and (not inside(x-1,y) or not inside(x+1,y) or not inside(x,y-1) or not inside(x,y+1)) end
local function ground(border,base)
  local dst=im()
  for y=0,N-1 do for x=0,N-1 do if inside(x,y) then put(dst,x,y,edge(x,y) and border or base) end end end
  return dst
end
local function flatten(images,w,h)
  local dst=im(w,h);for _,a in ipairs(images) do dst:drawImage(a) end;return dst
end
local function bubble(dst,x,y,stage)
  if stage==0 then put(dst,x,y,B[4]);put(dst,x+1,y,B[7])
  elseif stage==1 then
    line(dst,x,y-1,x+1,y-1,B[8]);put(dst,x-1,y,B[7]);put(dst,x+2,y,B[4]);line(dst,x,y+1,x+1,y+1,B[5])
  elseif stage==2 then
    line(dst,x-1,y-2,x+1,y-2,B[8]);line(dst,x-2,y-1,x-2,y+1,B[7]);line(dst,x+2,y-1,x+2,y+1,B[4]);line(dst,x-1,y+2,x+1,y+2,B[5])
  else
    put(dst,x-3,y,B[4]);put(dst,x+3,y,B[4]);line(dst,x-1,y+2,x+1,y+2,B[4])
  end
end
local function foamCluster(dst,x,y,w,h,f,seed)
  -- Rounded, connected cream foam with small liquid holes, not oily glints.
  ellipse(dst,x,y,w,h,B[6]);ellipse(dst,x,y-1,w,h-.5,B[7])
  ellipse(dst,x-w+2,y-1,2.3,1.8,B[7]);ellipse(dst,x+w-2,y-1,2.2,2.0,B[7])
  line(dst,x-w+3,y-2,x-1,y-2,B[8]);put(dst,x+2,y-2,B[8])
  put(dst,x-2,y+1,B[3]);put(dst,x+2,y,B[6]);put(dst,x+3,y,B[6])
  if (f+seed)%4==1 then put(dst,x+1,y,B[3]) elseif (f+seed)%4==2 then put(dst,x+1,y,B[6]);put(dst,x+1,y-1,B[3]) end
end
local function beer(f)
  local base=ground(B[1],B[2])
  poly(base,{{14,21},{24,17},{35,18},{39,22},{34,26},{25,25},{20,28},{13,27}},B[3])
  poly(base,{{29,35},{38,29},{47,31},{49,36},{44,41},{37,43},{30,40}},B[3])
  poly(base,{{17,39},{25,38},{28,42},{34,45},{32,49},{24,48},{20,44},{15,44}},B[3])
  -- Honey-gold swirls remain low contrast; no brown/black specular pools.
  poly(base,{{24,29},{31,27},{35,29},{33,31},{28,32},{24,34},{20,32}},B[5])
  local surface=im();local sh=({0,1,0,-1})[f+1]
  arc(surface,29,32,13,11,200+sh*3,247+sh*3,B[4])
  arc(surface,32,33,13,12,15-sh*4,53-sh*4,B[4])
  line(surface,28+sh,21,32+sh,21,B[4])
  line(surface,19,41-sh,22,41-sh,B[4])
  local foam=im()
  foamCluster(foam,20,18,6.2,2.7,f,0)
  foamCluster(foam,31,11,5.6,2.5,f,1)
  foamCluster(foam,44,17,4.1,3.0,f,2)
  foamCluster(foam,48,38,3.3,4.0,f,3)
  foamCluster(foam,21,47,5.9,2.5,f,2)
  foamCluster(foam,12,32,2.2,3.2,f,0)
  ellipse(foam,36,43,2.1,1.4,B[7]);put(foam,36,42,B[8])
  bubble(foam,24,30,f);bubble(foam,40,30,(f+2)%4)
  bubble(foam,31,39,(f+1)%4)
  for _,a in ipairs({base,surface,foam}) do clip(a) end
  return {base,surface,foam,im()}
end
local function ribbon(dst,points,widths,c)
  -- Broad curving strips viewed in the ground plane, never vertical flame icons.
  for k=1,#points-1 do
    local a,b=points[k],points[k+1];local dx,dy=b[1]-a[1],b[2]-a[2]
    local steps=math.max(math.abs(dx),math.abs(dy))*3
    for i=0,steps do
      local t=steps==0 and 0 or i/steps
      local radius=widths[k]*(1-t)+widths[k+1]*t
      ellipse(dst,a[1]+dx*t,a[2]+dy*t,math.max(.55,radius),math.max(.55,radius*.8),c)
    end
  end
end
local routes={
  {{15,24},{21,20},{27,21},{30,26},{36,26},{42,21},{44,17}},
  {{18,26},{17,31},{21,35},{27,34},{31,29},{36,31},{36,36}},
  {{44,25},{48,29},{45,34},{40,35},{38,40},{41,44},{46,43}},
  {{17,43},{23,47},{28,44},{29,40},{34,38},{38,41},{35,47}}
}
local function burn(f)
  local base=ground(F[1],F[2])
  poly(base,{{13,26},{22,22},{29,25},{32,30},{27,32},{22,29},{16,31}},F[1])
  poly(base,{{29,40},{35,35},{45,35},{48,39},{41,43},{35,44}},F[1])
  local sheets=im();local cores=im()
  local pulse=({0,1,1,0,-1,-1})[f+1]
  for k,route in ipairs(routes) do
    local pts={}
    for j,p in ipairs(route) do
      local wave=math.floor(math.sin((f/6+j*.22+k*.17)*math.pi*2)+.5)
      pts[j]={p[1]+wave,p[2]+(j%2==0 and pulse or -pulse)}
    end
    ribbon(sheets,pts,{2.8,3.8,3.5,3.0,2.4,1.7,.6,#pts==8 and .4 or nil},F[3])
    ribbon(sheets,pts,{1.6,2.5,2.1,1.7,1.3,.8,.55,#pts==8 and .4 or nil},F[4])
    -- Break the pale cores into moving curved pieces instead of solid rings.
    for j=1,#pts-1 do
      if (j+k+math.floor(f/2))%3~=0 then
        local a,b=pts[j],pts[j+1]
        local stop=.55+.1*((f+j)%3)
        line(cores,a[1],a[2],a[1]+(b[1]-a[1])*stop,a[2]+(b[2]-a[2])*stop,F[5])
        if (j+k+f)%4==0 then put(cores,a[1],a[2],F[7]);put(cores,a[1]+1,a[2],F[7]) end
      end
    end
  end
  -- Surface-edge heat flickers in short patches; footprint itself stays fixed.
  for y=0,N-1 do for x=0,N-1 do if edge(x,y) then
    local band=math.floor(x/4)+math.floor(y/5)
    if (band+f)%5<2 then put(sheets,x,y,F[4])
    elseif (band+f)%5==3 then put(base,x,y,F[6]) end
  end end end
  for _,a in ipairs({base,sheets,cores}) do clip(a) end
  return {base,sheets,cores,im()}
end
local beerFrames,fireFrames,igniteFrames={},{},{}
for f=0,3 do beerFrames[#beerFrames+1]={images=beer(f),ms=160} end
for f=0,5 do fireFrames[#fireFrames+1]={images=burn(f),ms=80} end
local radii={-20,5,11,18,26,34,41,100}
local counts={}
for f=0,7 do
  if f==0 then igniteFrames[1]={images=beerFrames[1].images,ms=80};counts[1]=0
  elseif f==7 then igniteFrames[8]={images=fireFrames[1].images,ms=80};counts[8]=-1
  else
    local before=beerFrames[f%4+1].images;local after=fireFrames[(f-1)%6+1].images
    local layers={im(),im(),im(),im()};local burned=0
    for y=0,N-1 do for x=0,N-1 do if inside(x,y) then
      local dx,dy=x-12,y-32
      local ang=math.atan(dy,dx)
      local distance=math.sqrt(dx*dx+dy*dy)+math.sin(ang*5)*1.8+math.sin(ang*11)*.8
      local d=radii[f+1]-distance
      if d>=0 then burned=burned+1 end
      for k=1,3 do put(layers[k],x,y,(d>=0 and after[k] or before[k]):getPixel(x,y)) end
      if d>=0 and d<1.3 then put(layers[4],x,y,F[7])
      elseif d>=1.3 and d<2.8 then put(layers[4],x,y,F[5])
      elseif d>=2.8 and d<4.2 then put(layers[4],x,y,F[4]) end
    end end end
    igniteFrames[f+1]={images=layers,ms=80};counts[f+1]=burned
  end
end
local particleFrames={}
local particleShapes={
  {'..2..','.232.','..2..'},
  {'...2.','..23.','.234.','.232.','..2..'},
  {'....2..','...22..','..232..','.2342..','22342..','.232...','..2....'},
  {'...2...','..23...','...2...','.......','..23...','.232...','..2....'},
  {'..2..','.....','....2','...2.'},
  {}
}
for f=0,5 do
  local dst=im(16,16);local pat=particleShapes[f+1]
  local bottom=14-math.floor(f*.7)
  for y,row in ipairs(pat) do for x=1,#row do
    local v=tonumber(row:sub(x,x));if v then put(dst,8+x-math.ceil(#row/2),bottom-#pat+y,({F[2],F[3],F[5],F[7]})[v]) end
  end end
  particleFrames[#particleFrames+1]={images={dst},ms=80}
end
local allPalette={};for _,a in ipairs({B,F}) do for _,c in ipairs(a) do
  local found=false;for _,v in ipairs(allPalette) do if v==c then found=true end end
  if not found then allPalette[#allPalette+1]=c end
end end
local function make(name,w,h,frames,pal,names,tags)
  local s=Sprite(w,h,ColorMode.RGB);s.gridBounds=Rectangle(0,0,1,1)
  local p=Palette(#pal+1);p:setColor(0,Color{r=0,g=0,b=0,a=0})
  for k,c in ipairs(pal) do p:setColor(k,c) end;s:setPalette(p)
  local layers={s.layers[1]};for k=2,#names do layers[k]=s:newLayer() end
  for k,l in ipairs(layers) do l.name=names[k] end
  for f,frame in ipairs(frames) do
    if f>1 then s:newEmptyFrame() end;s.frames[f].duration=frame.ms/1000
    for k,a in ipairs(frame.images) do
      local old=layers[k]:cel(f);if old then s:deleteCel(old) end
      s:newCel(layers[k],f,a,Point(0,0))
    end
    flatten(frame.images,w,h):saveAs(out..'/'..name..'-frame-'..f..'.png')
  end
  for _,a in ipairs(tags) do local tag=s:newTag(a[2],a[3]);tag.name=a[1] end
  local slice=s:newSlice(Rectangle(0,0,w,h));slice.name=w==64 and 'GroundCenter' or 'ParticleBase';slice.pivot=w==64 and Point(32,32) or Point(8,14)
  s.data='Aseprite native pixels. Flat ground; particles are independent. Preview only; no Unity integration.'
  s:saveAs(out..'/'..name..'.aseprite');s:saveCopyAs(out..'/'..name..'.gif')
  local cols=math.min(#frames,8);local sheet=im(w*cols,h*math.ceil(#frames/cols))
  for f,frame in ipairs(frames) do sheet:drawImage(flatten(frame.images,w,h),Point((f-1)%cols*w,math.floor((f-1)/cols)*h)) end
  sheet:saveAs(out..'/'..name..'-sheet.png')
  return s
end
local names={'01 Liquid / burning ground','02 Surface motion','03 Foam / surface cores','04 Ignition front'}
local sprites={}
sprites.beer=make('beer',64,64,beerFrames,B,names,{{'BeerIdle',1,4}})
sprites.fire=make('fire-surface',64,64,fireFrames,F,names,{{'FireLoop',1,6}})
sprites.ignite=make('beer-to-fire',64,64,igniteFrames,allPalette,names,{{'Ignite',1,8}})
sprites.particle=make('flame-particle',16,16,particleFrames,F,{'01 Particle only'},{{'ParticleLife',1,6}})
local master={};for _,frames in ipairs({beerFrames,igniteFrames}) do for _,fr in ipairs(frames) do master[#master+1]=fr end end
-- Ignite finishes on Fire frame1; start the master loop on frame2 to avoid a duplicate hold.
for i=1,6 do master[#master+1]=fireFrames[i%6+1] end
sprites.master=make('puddle-cycle',64,64,master,allPalette,names,{{'BeerIdle',1,4},{'Ignite',5,12},{'FireLoop',13,18}})
local function neutral(w,h)
  local dst=im(w,h)
  for y=0,h-1 do for x=0,w-1 do put(dst,x,y,((x%16==0 or y%16==0) and rgba('2B343A') or rgba('343E45'))) end end
  return dst
end
local function preview(name,w,h,draw,count,ms,scale)
  local s=Sprite(w,h,ColorMode.RGB);s.layers[1].name='Preview - neutral ground, not Unity capture'
  for f=0,count-1 do
    if f>0 then s:newEmptyFrame() end;s.frames[f+1].duration=ms/1000
    local a=draw(f*ms);local old=s.layers[1]:cel(f+1);if old then s:deleteCel(old) end
    s:newCel(s.layers[1],f+1,a,Point(0,0))
  end
  s:resize(w*scale,h*scale);s:saveAs(out..'/'..name..'.aseprite');s:saveCopyAs(out..'/'..name..'.gif')
  Image(s):saveAs(out..'/'..name..'.png');s:close()
end
local function stageAt(t)
  if t<640 then return beerFrames[math.floor(t/160)%4+1]
  elseif t<1280 then return igniteFrames[math.floor((t-640)/80)+1]
  elseif t<3520 then return fireFrames[(math.floor((t-1280)/80)+1)%6+1]
  else return nil end -- A visible blank reset separates repetitions of the demonstration.
end
local function particles(dst,t,ox,oy)
  for _,p in ipairs({{20,29,0,-1},{42,41,320,1},{35,23,640,0}}) do
    local age=(t+p[3])%960
    if age<480 then
      local frame=particleFrames[math.floor(age/80)+1]
      local rise=math.floor(age/100);local drift=math.floor(age/200)*p[4]
      dst:drawImage(frame.images[1],Point(ox+p[1]-8+drift,oy+p[2]-14-rise))
    end
  end
end
preview('comparison-preview',240,80,function(t)
  local dst=neutral(240,80)
  line(dst,79,0,79,79,rgba('52616B'));line(dst,159,0,159,79,rgba('52616B'))
  dst:drawImage(flatten(beerFrames[math.floor(t/160)%4+1].images),Point(8,8))
  local current=stageAt(t);if current then dst:drawImage(flatten(current.images),Point(88,8)) end
  dst:drawImage(flatten(fireFrames[math.floor(t/80)%6+1].images),Point(168,8));particles(dst,t,168,8)
  return dst
end,96,40,4)
preview('ignition-preview',80,80,function(t)
  local dst=neutral(80,80);local current=stageAt(t)
  if current then dst:drawImage(flatten(current.images),Point(8,8)) end
  return dst
end,96,40,4)
preview('surface-only-preview',80,80,function(t)
  local dst=neutral(80,80);dst:drawImage(flatten(fireFrames[math.floor(t/80)%6+1].images),Point(8,8));return dst
end,6,80,4)
local contact=neutral(8*64,64*3)
for i,fr in ipairs(beerFrames) do contact:drawImage(flatten(fr.images),Point((i-1)*64,0)) end
for i,fr in ipairs(igniteFrames) do contact:drawImage(flatten(fr.images),Point((i-1)*64,64)) end
for i,fr in ipairs(fireFrames) do contact:drawImage(flatten(fr.images),Point((i-1)*64,128)) end
contact:resize(1024,384);contact:saveAs(out..'/all-frames-2x.png')
local partSheet=neutral(96,24)
for i,fr in ipairs(particleFrames) do partSheet:drawImage(fr.images[1],Point((i-1)*16,4)) end
partSheet:resize(576,144);partSheet:saveAs(out..'/particle-frames-6x.png')
for _,s in pairs(sprites) do s:close() end
local log=assert(io.open(out..'/authoring-complete.txt','w'))
log:write('Aseprite '..tostring(app.version)..'\nBeer4 / Ignite8 / Fire6 / Particle6\nNative64x64 ground,16x16 particle; ground pivot32,32.\nIgnition burned pixel counts: '..table.concat(counts,',')..'\n')
log:close()
