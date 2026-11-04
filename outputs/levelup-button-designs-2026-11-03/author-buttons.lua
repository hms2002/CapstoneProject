-- Native Aseprite pixel authoring. Read-only project inputs, output-only writes.
local root,out=app.params.root,app.params.output
local pc=app.pixelColor
local function c(h,a) return pc.rgba(tonumber(h:sub(1,2),16),tonumber(h:sub(3,4),16),tonumber(h:sub(5,6),16),a or 255) end
local function rect(im,x,y,w,h,col)
  for yy=math.max(0,y),math.min(im.height-1,y+h-1) do
    for xx=math.max(0,x),math.min(im.width-1,x+w-1) do im:drawPixel(xx,yy,col) end
  end
end
local function bevel(im,x,y,w,h,cut,col)
  for yy=0,h-1 do
    local d=math.max(0,cut-yy,cut-(h-1-yy))
    rect(im,x+d,y+yy,w-2*d,1,col)
  end
end
local function resize(im,w,h)
  local r=Image(w,h)
  for p in r:pixels() do p(im:getPixel(math.floor(p.x*im.width/w),math.floor(p.y*im.height/h))) end
  return r
end
local function crop(im,x,y,w,h)
  local r=Image(w,h);r:drawImage(im,Point(-x,-y));return r
end
local function tint(im,col)
  local r=Image(im)
  for p in r:pixels() do local v=p();p(pc.rgba(math.floor(pc.rgbaR(v)*pc.rgbaR(col)/255),math.floor(pc.rgbaG(v)*pc.rgbaG(col)/255),math.floor(pc.rgbaB(v)*pc.rgbaB(col)/255),pc.rgbaA(v))) end
  return r
end
local typecache={}
local function typ(name)
  if not typecache[name] then typecache[name]=Image{fromFile=out..'/typography/'..name..'.png'} end
  return typecache[name]
end
local function centered(im,name,x,y,w,h)
  local t=typ(name);im:drawImage(t,Point(math.floor(x+(w-t.width)/2),math.floor(y+(h-t.height)/2)))
end
local function add(sp,name,im,pos,frame)
  local l=sp:newLayer();l.name=name;sp:newCel(l,frame or 1,im,pos or Point(0,0));return l
end
local variants={
 {id='A-gilt',name='A - Antique gilt',rim='B88D52',hi='F1D49B',body='28232D',low='4D3B35',key='1C1923',count='3B2F2B'},
 {id='B-parchment',name='B - Parchment',rim='82583B',hi='FFF0BD',body='DFC894',low='AD8254',key='EEDDAD',count='C2A56E'},
 {id='C-iron',name='C - Forged iron',rim='7F8C99',hi='D0D8D5',body='333C48',low='1A222E',key='1F2835',count='202732'},
 {id='D-arcane',name='D - Arcane crystal',rim='8976B8',hi='D8CEFF',body='28233F',low='44355F',key='1C1930',count='203842'},
 {id='E-card-tab',name='E - Card tab',rim='99826B',hi='F0DFC0',body='DFD2AE',low='594248',key='CEC3A3',count='C3AF88'}
}
local pairNormal,normalLayers={},{}
local function button(k,isRoll,state)
  local v=variants[k];local w=isRoll and 170 or 160
  local surf,detail,txt=Image(w,40),Image(w,40),Image(w,40)
  local dy=state==3 and 2 or 0
  local ink=c('13121B');local rim=c(v.rim);local hi=c(v.hi);local body=c(v.body)
  if not isRoll and k==1 then rim=c('776B62');hi=c('A99B82') end
  if isRoll and k==3 then rim=c('B49A6E');hi=c('E1D5B7') end
  if isRoll and k==4 then rim=c('5CAAA7');hi=c('BCEADE') end
  if state==2 then rim=hi end
  if state==4 then ink=c('17171E');rim=c('55515E');hi=c('6E6876');body=c('302C38') end
  bevel(surf,0,3,w,37,4,ink)
  bevel(surf,0,dy,w,37,4,rim)
  bevel(surf,2,2+dy,w-4,33,3,c(v.low))
  bevel(surf,3,3+dy,w-6,29,2,body)
  rect(detail,5,2+dy,w-10,1,hi)
  rect(detail,4,5+dy,1,24,hi)
  if k==1 then
    rect(detail,6,5+dy,w-12,1,c('4A3B38'))
    for _,x in ipairs({7,w-10}) do
      rect(detail,x,8+dy,3,1,rim);rect(detail,x+1,7+dy,1,3,rim)
      rect(detail,x,28+dy,3,1,rim);rect(detail,x+1,27+dy,1,3,rim)
    end
  elseif k==2 then
    rect(detail,7,6+dy,w-14,1,c('F3DDA4'))
    rect(detail,8,30+dy,w-16,1,c('BE9B68'))
    for _,x in ipairs({6,w-8}) do rect(detail,x,12+dy,2,12,c('C2A36F')) end
    if isRoll then rect(detail,w-15,0+dy,6,6,c('994953'));rect(detail,w-14,0+dy,4,4,c('C9706C')) end
  elseif k==3 then
    rect(detail,6,4+dy,w-12,2,c('515E6C'))
    rect(detail,6,29+dy,w-12,3,c('252D3A'))
    for _,x in ipairs({6,w-9}) do for _,y in ipairs({7,27}) do
      rect(detail,x,y+dy,3,3,ink);rect(detail,x,y+dy,2,2,hi)
    end end
  elseif k==4 then
    rect(detail,9,5+dy,w-18,1,c('493D66'))
    rect(detail,9,30+dy,w-18,1,c('493D66'))
    for _,x in ipairs({6,w-7}) do
      rect(detail,x-1,17+dy,3,3,rim);rect(detail,x,16+dy,1,5,hi)
    end
    rect(detail,18,0+dy,11,1,hi);rect(detail,w-30,36+dy,11,1,hi)
  else
    local band=isRoll and c('8C354D') or c('62525B')
    rect(detail,5,3+dy,w-10,5,band)
    rect(detail,5,8+dy,w-10,1,c('B5A187'))
    rect(detail,w-15,4+dy,7,10,band)
    rect(detail,w-14,5+dy,5,8,isRoll and c('B55469') or c('80717B'))
    rect(detail,7,32+dy,w-14,1,c('B8A17B'))
  end
  local keyX=isRoll and 16 or 30
  local keyW=isRoll and 20 or 30
  local keyY=k==5 and 13 or 10
  bevel(detail,keyX,keyY+dy,keyW,18,1,c(v.low))
  bevel(detail,keyX+1,keyY+1+dy,keyW-2,15,1,c(v.key))
  rect(detail,keyX+2,keyY+1+dy,keyW-4,1,rim)
  local ti=state==4 and 6 or k
  centered(txt,ti..(isRoll and '-r' or '-esc'),keyX,keyY+dy,keyW,16)
  centered(txt,ti..(isRoll and '-reroll' or '-close'),isRoll and 49 or 72,keyY-1+dy,42,19)
  if isRoll then
    bevel(detail,113,keyY+dy,40,18,2,c(v.low))
    bevel(detail,114,keyY+1+dy,38,15,1,c(v.count))
    centered(txt,ti..(state==4 and '-zero' or '-count'),115,keyY+dy,36,17)
  end
  if state==4 then
    surf=tint(surf,c('9D9AA4'));detail=tint(detail,c('8B8695'))
  end
  return {surf,detail,txt}
end
for k,v in ipairs(variants) do
  local sp=Sprite(345,40,ColorMode.RGB)
  sp.layers[1].name='01 - Body and bevel'
  local ls={sp.layers[1],sp:newLayer(),sp:newLayer()}
  ls[2].name='02 - Border, keycaps and count badge';ls[3].name='03 - Galmuri9 labels (raster)'
  for state=1,4 do
    if state>1 then sp:newEmptyFrame() end
    sp.frames[state].duration=1
    local close,roll=button(k,false,state),button(k,true,state)
    for j=1,3 do
      local im=Image(345,40);im:drawImage(close[j],Point(0,0));im:drawImage(roll[j],Point(175,0))
      sp:newCel(ls[j],state,im,Point(0,0))
      if state==1 then normalLayers[k]=normalLayers[k] or {};normalLayers[k][j]=im end
    end
    local names={'normal','focus','pressed','disabled'}
    sp:newTag(state,state).name=names[state]
    local flat=Image(345,40);flat:drawSprite(sp,state)
    flat:saveAs(out..'/'..v.id..'-'..names[state]..'.png')
    if state==1 then pairNormal[k]=flat end
  end
  sp:saveAs(out..'/'..v.id..'-buttons.aseprite');sp:close()
end

-- Screen context from the authored prefab: 1920x1080, cards 472x740,
-- footer centers (-180,80)/(180,80), sizes 320x80/340x80.
local sourceDir=root..'/Assets/_Project/Art/Sprites/UI/LevelRewardCards/'
local bg=resize(Image{fromFile=root..'/outputs/localization-screen-review-2026-10-04/ko-hub.png'},1920,1080)
bg=tint(bg,c('383838'))
rect(bg,640,40,640,100,c('080808'))
centered(bg,'title',640,40,640,100)
local cards=Image(1920,1080)
local styles={'Void_Card_1','Thunder_Card_9','Earth_Card_3'}
for i,name in ipairs(styles) do
  local src=Image{fromFile=sourceDir..name..'.png'}
  local card=crop(src,112,src.height-160-128,80,128)
  for _,layer in ipairs({{'Base','20221F'},{'Description','FFFBDC'},{'Title_1','FFFFFF'},{'Outline_1','B2285E'}}) do
    local tex=Image{fromFile=sourceDir..layer[1]..'.png'}
    local cropped=crop(tex,16,tex.height-8-128,80,128)
    card:drawImage(tint(cropped,c(layer[2])),Point(0,0))
  end
  cards:drawImage(resize(card,472,740),Point(172+(i-1)*552,175))
end
local cardType=Image(1920,1080)
for i,count in ipairs({4,3,2}) do
  local x=172+(i-1)*552
  centered(cardType,'card-'..i..'-title',x+35,592,402,50)
  local start=662+(208-count*42)/2
  for line=1,count do centered(cardType,'card-'..i..'-'..line,x+35,start+(line-1)*42,402,40) end
end
local base=Image(bg);base:drawImage(cards,Point(0,0));base:drawImage(cardType,Point(0,0))
local current=Image(base)
rect(current,620,960,320,80,c('D1D1D1'));rect(current,970,960,340,80,c('D1D1D1'))
centered(current,'current-close',620,960,320,80);centered(current,'current-reroll',970,960,340,80)
current:saveAs(out..'/00-current-reconstructed.png')
local screens={current}
for i,v in ipairs(variants) do
  local sp=Sprite(1920,1080,ColorMode.RGB)
  sp.layers[1].name='Existing game background - dimmed reference'
  sp:newCel(sp.layers[1],1,bg,Point(0,0))
  add(sp,'Original card artwork - prefab placement',cards)
  add(sp,'Sample reward text - Galmuri9',cardType)
  for j,name in ipairs({'Button bodies','Button detail and keycaps','Button text and count'}) do add(sp,name,resize(normalLayers[i][j],690,80),Point(620,960)) end
  sp:saveAs(out..'/'..v.id..'-screen.aseprite')
  local flat=Image(sp);flat:saveAs(out..'/'..v.id..'-screen.png');screens[#screens+1]=flat
  sp:close()
end

-- Large comparison board for immediate design review.
local board=Sprite(1500,1110,ColorMode.RGB)
board.layers[1].name='Comparison layout'
local canvas=Image(1500,1110);rect(canvas,0,0,1500,1110,c('14151E'))
canvas:drawImage(typ('board-title'),Point(44,38));canvas:drawImage(typ('board-sub'),Point(46,100))
rect(canvas,44,146,1412,1,c('37323C'))
for i,v in ipairs(variants) do
  local y=173+(i-1)*168
  bevel(canvas,32,y,1436,150,8,c('1F202B'))
  rect(canvas,32,y+15,3,120,c(v.rim))
  canvas:drawImage(typ('option-'..i),Point(58,y+27));canvas:drawImage(typ('desc-'..i),Point(58,y+73))
  for j,h in ipairs({v.body,v.low,v.rim,v.hi}) do rect(canvas,59+(j-1)*28,y+111,20,6,c(h)) end
  add(board,v.name,resize(pairNormal[i],1035,120),Point(408,y+15))
end
canvas:drawImage(typ('board-foot'),Point(45,1065))
board:newCel(board.layers[1],1,canvas,Point(0,0))
board:saveAs(out..'/01-button-comparison.aseprite');Image(board):saveAs(out..'/01-button-comparison.png');board:close()

local contact=Sprite(1920,980,ColorMode.RGB)
contact.layers[1].name='Screen comparison layout'
local cb=Image(1920,980);rect(cb,0,0,1920,980,c('14151E'))
cb:drawImage(typ('screen-board-title'),Point(32,24));cb:drawImage(typ('screen-board-sub'),Point(34,77))
for i,screen in ipairs(screens) do
  local x=((i-1)%3)*640;local y=130+math.floor((i-1)/3)*420
  cb:drawImage(typ('caption-'..(i-1)),Point(x+20,y))
  add(contact,'Screen '..(i-1),resize(screen,624,351),Point(x+8,y+46))
end
contact:newCel(contact.layers[1],1,cb,Point(0,0))
contact:saveAs(out..'/02-screen-comparison.aseprite');Image(contact):saveAs(out..'/02-screen-comparison.png');contact:close()
print('Aseprite '..tostring(app.version)..': five layered button sets (4 states), five layered screen mockups, and two comparison boards exported.')
