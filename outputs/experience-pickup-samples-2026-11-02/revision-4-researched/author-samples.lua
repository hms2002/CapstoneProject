-- New, original XP pickup studies after a reference review. Earlier shapes are not reused.
local output=assert(app.params.output,'output directory required')
local rgba=app.pixelColor.rgba
local function color(h)
  return rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),255)
end
local variants={
  {id='01-azure-shard',name='AZURE SHARD',size='7 X 10',desc='CRYSTAL',
    colors={'193D68','287DC6','65DCEF','DCFFF7'},rows={
      '...o...',
      '..olo..',
      '.olcmo.',
      'olccmmo',
      'olclmmo',
      'olclmmo',
      'olllmmo',
      '.olmmo.',
      '..omo..',
      '...o...'
    }},
  {id='02-lumen-orb',name='LUMEN ORB',size='8 X 8',desc='ENERGY',
    colors={'385D32','75B84A','C4E766','F4FFBC'},rows={
      '..oooo..',
      '.ommmmo.',
      'omllllmo',
      'omlcclmo',
      'omlcclmo',
      'omllllmo',
      '.ommmmo.',
      '..oooo..'
    }},
  {id='03-soul-mote',name='SOUL MOTE',size='8 X 10',desc='ESSENCE',
    colors={'174653','258E9B','67DEDA','DCFFF0'},rows={
      '.....ll.',
      '....lcl.',
      '...llc..',
      '...lcc..',
      '..lccml.',
      '.lccmmlo',
      'olccmmlo',
      'olllmmmo',
      '.ommmmo.',
      '..oooo..'
    }},
  {id='04-amber-grain',name='AMBER GRAIN',size='6 X 6',desc='TINY DROP',
    colors={'724526','D38B32','F9CA62','FFF3B6'},rows={
      '..oo..',
      '.olmo.',
      'olclmo',
      'olcmmo',
      '.ommo.',
      '..oo..'
    }},
  {id='05-arcane-cluster',name='ARCANE DUST',size='11 X 9',desc='FRAGMENTS',
    colors={'453166','9555C5','D99BEF','FFE1FF'},rows={
      '......o....',
      '.....olo...',
      '....olcmo..',
      '.....omo...',
      '..o...o....',
      '.olo.....o.',
      'olcmo...olo',
      '.omo...olmo',
      '..o.....oo.'
    }}
}
local function rect(im,x,y,w,h,c)
  for yy=y,y+h-1 do for xx=x,x+w-1 do im:drawPixel(xx,yy,c) end end
end
local function stamp(dst,src,x,y,scale)
  for yy=0,src.height-1 do for xx=0,src.width-1 do
    local c=src:getPixel(xx,yy)
    if app.pixelColor.rgbaA(c)>0 then rect(dst,x+xx*scale,y+yy*scale,scale,scale,c) end
  end end
end
local shifts={0,0,-1,-1,0,0,1,1}
local composites={}
for i,v in ipairs(variants) do
  local p={o=color(v.colors[1]),m=color(v.colors[2]),l=color(v.colors[3]),c=color(v.colors[4])}
  local s=Sprite(16,16,ColorMode.RGB)
  s.layers[1].name='Silhouette and color'
  local core=s:newLayer();core.name='Emissive center / accent'
  local outline,accent=Image(16,16),Image(16,16)
  local w=0
  for _,r in ipairs(v.rows) do w=math.max(w,#r) end
  local ox,oy=math.floor((16-w)/2),math.floor((16-#v.rows)/2)
  for y,r in ipairs(v.rows) do for x=1,#r do
    local k=r:sub(x,x)
    if p[k] then
      if k=='c' then accent:drawPixel(ox+x-1,oy+y-1,p[k])
      else outline:drawPixel(ox+x-1,oy+y-1,p[k]) end
    end
  end end
  composites[i]={}
  for f=1,8 do
    if f>1 then s:newEmptyFrame() end
    s.frames[f].duration=.13
    s:newCel(s.layers[1],f,outline,Point(0,shifts[f]))
    s:newCel(core,f,accent,Point(0,shifts[f]))
    local merged=Image(16,16)
    merged:drawImage(outline,Point(0,shifts[f]));merged:drawImage(accent,Point(0,shifts[f]))
    composites[i][f]=merged
  end
  local tag=s:newTag(1,8);tag.name='Idle float'
  s:saveAs(output..'/'..v.id..'.aseprite')
  composites[i][1]:saveAs(output..'/'..v.id..'.png')
  local sheet=Image(128,16)
  for f=1,8 do stamp(sheet,composites[i][f],(f-1)*16,0,1) end
  sheet:saveAs(output..'/'..v.id..'-idle-sheet.png')
  local enlarged=Image(192,192);stamp(enlarged,composites[i][1],0,0,12)
  enlarged:saveAs(output..'/'..v.id..'-12x.png')
end

local glyphs={
 A={'010','101','111','101','101'},B={'110','101','110','101','110'},C={'011','100','100','100','011'},
 D={'110','101','101','101','110'},E={'111','100','110','100','111'},F={'111','100','110','100','100'},
 G={'011','100','101','101','011'},H={'101','101','111','101','101'},I={'111','010','010','010','111'},
 J={'001','001','001','101','010'},K={'101','101','110','101','101'},L={'100','100','100','100','111'},
 M={'101','111','111','101','101'},N={'101','111','111','111','101'},O={'010','101','101','101','010'},
 P={'110','101','110','100','100'},Q={'010','101','101','111','011'},R={'110','101','110','101','101'},
 S={'011','100','010','001','110'},T={'111','010','010','010','010'},U={'101','101','101','101','111'},
 V={'101','101','101','101','010'},W={'101','101','111','111','101'},X={'101','101','010','101','101'},
 Y={'101','101','010','010','010'},Z={'111','001','010','100','111'},
 ['0']={'111','101','101','101','111'},['1']={'010','110','010','010','111'},['2']={'110','001','010','100','111'},
 ['3']={'110','001','010','001','110'},['4']={'101','101','111','001','001'},['5']={'111','100','110','001','110'},
 ['6']={'011','100','110','101','010'},['7']={'111','001','010','100','100'},['8']={'010','101','010','101','010'},
 ['9']={'010','101','011','001','110'},['/']={'001','001','010','100','100'},['.']={'000','000','000','000','010'}
}
local function text(im,s,x,y,c,scale)
  scale=scale or 1
  for i=1,#s do
    local g=glyphs[s:sub(i,i)]
    if g then for yy=1,5 do for xx=1,3 do
      if g[yy]:sub(xx,xx)=='1' then rect(im,x+(xx-1)*scale,y+(yy-1)*scale,scale,scale,c) end
    end end end
    x=x+4*scale
  end
end
local function centered(im,s,cx,y,c) text(im,s,math.floor(cx-(#s*4-1)/2),y,c) end
local board=Sprite(460,264,ColorMode.RGB)
board.layers[1].name='Study board / preview only'
local bg=Image(460,264)
rect(bg,0,0,460,264,color('171C29'))
text(bg,'EXPERIENCE / PICKUPS',14,13,color('F2E9D8'),2)
text(bg,'FIVE NEW DIRECTIONS',302,15,color('8D9EB3'))
text(bg,'16PX / ASEPRITE',302,25,color('8D9EB3'))
rect(bg,14,38,432,1,color('394252'))
for i,v in ipairs(variants) do
  local x=14+(i-1)*88
  rect(bg,x,49,80,190,color('222B3A'))
  rect(bg,x,49,80,1,color(v.colors[3]))
  text(bg,'0'..i,x+7,58,color(v.colors[3]))
  text(bg,v.desc,x+22,58,color('99A7B6'))
  centered(bg,v.name,x+40,141,color('E8E4D9'))
  centered(bg,v.size,x+40,153,color('99A7B6'))
  rect(bg,x+6,169,68,25,color('121923'))
  rect(bg,x+6,200,68,25,color('7F7563'))
  for k=1,4 do rect(bg,x+24+(k-1)*8,231,6,2,color(v.colors[k])) end
end
text(bg,'ACTUAL PIXEL CLUSTERS ON DARK / STONE BELOW',14,249,color('99A7B6'))
local layers={}
for i,v in ipairs(variants) do local l=board:newLayer();l.name=v.name;layers[i]=l end
for f=1,8 do
  if f>1 then board:newEmptyFrame() end
  board.frames[f].duration=.13
  board:newCel(board.layers[1],f,bg,Point(0,0))
  local final=Image(460,264);final:drawImage(bg)
  for i,v in ipairs(variants) do
    local x=14+(i-1)*88
    local im=Image(460,264)
    stamp(im,composites[i][f],x+8,72,4)
    for j,xx in ipairs({9,32,55}) do
      local phase=(f+j*2+i-2)%8+1
      stamp(im,composites[i][phase],x+xx,174,1)
      stamp(im,composites[i][phase],x+xx,205,1)
    end
    board:newCel(layers[i],f,im,Point(0,0));final:drawImage(im)
  end
  if f==1 then
    final:saveAs(output..'/xp-new-directions.png')
    local large=Image(1380,792);stamp(large,final,0,0,3)
    large:saveAs(output..'/xp-new-directions-preview.png')
  end
end
board:saveAs(output..'/xp-new-directions.aseprite')
print('Wrote five new pickup designs, eight-frame native sources, sheets, and comparison board.')
