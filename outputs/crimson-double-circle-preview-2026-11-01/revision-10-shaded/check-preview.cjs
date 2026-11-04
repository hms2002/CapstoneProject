const fs=require('node:fs'),vm=require('node:vm'),path=require('node:path'),assert=require('node:assert/strict');
const out=__dirname,html=fs.readFileSync(process.argv[2],'utf8');
const W=240,H=180,CX=120,CY=90;
const source=html.match(/<script>([\s\S]*?)<\/script>/)[1];
const paletteCounts={};
assert.ok(!source.includes('Math.atan2'),'No angular fill mask');
assert.ok(Buffer.byteLength(html)<1000000,'Fragment size');
const images=['outer','inner'].map(name=>{
  const png=fs.readFileSync(path.join(out,name+'-preview.png'));
  const size=name==='inner'?80:120;
  assert.equal(png.readUInt32BE(16),size);assert.equal(png.readUInt32BE(20),size);
  assert.ok(html.includes(png.toString('base64')),'Embedded artwork matches checked pixel source');
  const bytes=new Uint8ClampedArray(fs.readFileSync(path.join(out,name+'.rgba')));
  const borders=fs.readFileSync(path.join(out,name+'-borders.rgba'));
  const old=fs.readFileSync(path.join(out,'../revision-9-bold',name+'.rgba'));
  const palette=new Map([['255,198,90',0],['255,240,191',0],['241,107,50',0]]);
  for(let i=0;i<bytes.length;i+=4){
    assert.ok(bytes[i+3]===0||bytes[i+3]===255);
    if(bytes[i+3]){const rgb=Array.from(bytes.slice(i,i+3)).join(',');assert.ok(palette.has(rgb),'Exactly the three authored colors');palette.set(rgb,palette.get(rgb)+1);}
    if(borders[i+3])assert.deepEqual(Array.from(bytes.slice(i,i+4)),[255,198,90,255],'Every border pixel remains opaque gold');
    if(name==='inner')assert.equal(bytes[i+3],old[i+3],'Inner silhouette unchanged');
  }
  for(const count of palette.values())assert.ok(count>0,'All three colors used');
  paletteCounts[name]=Object.fromEntries(palette);
  if(name==='outer'){
    const sectors=Array(16).fill(0);
    for(let y=0;y<size;y++)for(let x=0;x<size;x++){
      const i=(y*size+x)*4;if(!bytes[i+3]||borders[i+3])continue;
      const angle=(Math.atan2(y-59.5,x-59.5)+Math.PI/2+Math.PI*2)%(Math.PI*2),unit=angle/(Math.PI/8);
      const nearest=Math.round(unit);assert.ok(Math.abs(unit-nearest)<.3,'Rune spacing leaves clear gaps between sectors');sectors[nearest%16]++;
    }
    assert.ok(sectors.every(n=>n>=20),'Sixteen distinct outer runes');
  }
  for(const [x,y] of [[size/2,0],[size/2,1],[0,size/2],[1,size/2]])assert.equal(bytes[(y*size+x)*4+3],255,'Outer border has two authored pixels at cardinal axes');
  return{naturalWidth:size,naturalHeight:size,pixels:bytes,decode:()=>Promise.resolve()};
});
let pixels,callback,input,click,changeLayer;
const ctx={createImageData:(w,h)=>({data:new Uint8ClampedArray(w*h*4)}),putImageData:im=>pixels=Buffer.from(im.data)};
const button={textContent:'',setAttribute:()=>{},addEventListener:(event,fn)=>click=fn};
const slider={value:'35',addEventListener:(event,fn)=>input=fn},value={textContent:''},phase={textContent:''};
const layerPicker={value:'all',addEventListener:(event,fn)=>changeLayer=fn};
const elements={'canvas':{getContext:()=>ctx},'[data-action="pause"]':button,'#crimson-scale-amount':slider,'[data-value="amount"]':value,'[data-value="phase"]':phase,'[data-control="layer"]':layerPicker};
const root={dataset:{},querySelector:q=>{assert.ok(elements[q],q);return elements[q];},querySelectorAll:()=>images};
const context=vm.createContext({Uint8ClampedArray,Promise,Math,Number,Infinity,
 document:{getElementById:id=>{assert.ok(html.includes('id="'+id+'"'));return root;},createElement:()=>{let image;return{getContext:()=>({drawImage:im=>image=im,getImageData:()=>({data:new Uint8ClampedArray(image.pixels)})})};}},
 window:{addEventListener:()=>{}},matchMedia:()=>({matches:false}),requestAnimationFrame:fn=>callback=fn});
new vm.Script(source).runInContext(context);
setImmediate(()=>{
  assert.equal(root.dataset.ready,'true');
  const stages=[0,25,50,75,100],radii=[],frames={};
  const expectedInner=[0,20,40,40,40],expectedOuter=[0,0,0,50,60];
  for(let n=0;n<stages.length;n++){
    const p=stages[n];slider.value=String(p);input();assert.equal(value.textContent,p+'%');
    const inner=Number(root.dataset.innerRadius),outer=Number(root.dataset.outerRadius);
    assert.ok(Math.abs(inner-expectedInner[n])<1e-8);assert.ok(Math.abs(outer-expectedOuter[n])<1e-8);
    radii.push({charge:p,inner,outer});frames[p]=Buffer.from(pixels);fs.writeFileSync(path.join(out,'charge-'+p+'.rgba'),pixels);
    const limit=Math.max(inner,outer)*1.015+1;
    for(let y=0;y<H;y++)for(let x=0;x<W;x++){
      const i=(y*W+x)*4;
      if(Math.hypot(x+.5-CX,y+.5-CY)>limit)assert.ok(pixels.subarray(i,i+4).equals(frames[0].subarray(i,i+4)),'No pixels beyond scaled boundary');
    }
  }
  function brightNear(frame,x,y){
    for(let yy=Math.floor(y)-1;yy<=Math.ceil(y)+1;yy++)for(let xx=Math.floor(x)-1;xx<=Math.ceil(x)+1;xx++){
      const i=(yy*W+xx)*4;if(frame[i]>190&&frame[i+1]>90)return true;
    }
    return false;
  }
  for(const [p,r] of [[25,20],[75,50]])for(const [dx,dy] of [[r,0],[0,r],[-r,0],[0,-r]])
    assert.ok(brightNear(frames[p],CX-.5+dx,CY-.5+dy),'Full circumference at '+p+'%, no radial clipping');
  function brightMask(frame){const mask=[];for(let i=0;i<frame.length;i+=4)mask.push(frame[i]>190&&frame[i+1]>90);return mask;}
  layerPicker.value='inner';changeLayer();assert.equal(root.dataset.layer,'inner');
  assert.deepEqual(brightMask(pixels),brightMask(frames[50]),'Entire inner design is complete at 50%');
  fs.writeFileSync(path.join(out,'layer-inner.rgba'),pixels);
  const innerCount=brightMask(pixels).filter(Boolean).length;
  assert.equal(innerCount,Array.from(images[1].pixels).filter((v,i)=>i%4===3&&v===255).length,'Inner displayed one logical pixel per source pixel');
  layerPicker.value='outer';changeLayer();assert.equal(root.dataset.layer,'outer');
  fs.writeFileSync(path.join(out,'layer-outer.rgba'),pixels);
  const outerCount=brightMask(pixels).filter(Boolean).length;
  assert.equal(outerCount,Array.from(images[0].pixels).filter((v,i)=>i%4===3&&v===255).length,'Outer displayed one logical pixel per source pixel');
  for(let y=0;y<H;y++)for(let x=0;x<W;x++)if(Math.hypot(x+.5-CX,y+.5-CY)<42.5){const i=(y*W+x)*4;assert.ok(pixels[i]<190,'Separate outer ring has no inner artwork');}
  layerPicker.value='all';changeLayer();
  slider.value='75';input();const before={outer:Number(root.dataset.outerAngle),inner:Number(root.dataset.innerAngle)};
  click();callback(0);callback(40);const advancing=Buffer.from(pixels);callback(80);
  assert.notDeepEqual(pixels,advancing);assert.ok(Number(root.dataset.outerAngle)>before.outer);assert.ok(Number(root.dataset.innerAngle)<before.inner);
  click();const frozen=Buffer.from(pixels),angles=[root.dataset.innerAngle,root.dataset.outerAngle];
  callback(120);callback(160);assert.deepEqual(pixels,frozen);assert.deepEqual([root.dataset.innerAngle,root.dataset.outerAngle],angles);
  click();callback(200);assert.notDeepEqual(pixels,frozen);
  slider.value='100';input();click();
  for(let now=240;now<=640;now+=40)callback(now);
  fs.writeFileSync(path.join(out,'charge-rotated.rgba'),pixels);
  const meta=fs.readFileSync(path.join(process.cwd(),'Assets/_Project/Art/Sprites/VFX/CrimsonBoundary/ChargeSixColor/Frames/impact_core_01.png.meta'),'utf8');
  const charge=fs.readFileSync(path.join(process.cwd(),'Assets/_Project/Data/Items/Weapons/LogicData/ALData_CrimsonBoundaryCharge.asset'),'utf8');
  assert.match(meta,/spritePixelsToUnits: 16/);assert.match(meta,/filterMode: 0/);assert.match(charge,/maximumDiameter: 6/);
  const report={radii,nativePixelSizes:{inner:80,outer:120,proposedPpu:20,existingCrimsonPpu:16,maximumDiameter:6},worldDiameterPreservation:'120 / 20 = 96 / 16 = 6; proposed import, not applied to Unity',nativeSourcePixelMapping:'pass',palette:'FFC65A gold, FFF0BF cream, F16B32 ember; binary alpha',paletteCounts,allBorderPixels:'Uniform gold verified',innerSilhouette:'Identical to v9',cardinalBorderThickness:'2px verified',regularRunes:'7x7 bold; inner 12 plus 4 center marks; outer 16, separated sectors verified',wholeInnerCompletedAt50:'pass',separateLayers:'pass',innerOpaquePixels:innerCount,outerOpaquePixels:outerCount,fullCircumferenceAtPartialCharge:'pass',scaledBoundary:'pass',oppositePlanarRotation:'pass',pauseResume:'pass',artwork:'Gold borders preserved; rune cap/foot and octagram vertex accents; outer count 12 to 16',rendering:'offline same-JavaScript Canvas pixel adapter',limitations:'Browser DOM/layout and Unity Play Mode not run. The 20 PPU proposal is denser than existing 16 PPU effects. Point filtering alone does not guarantee stable lines at every rotation/scale or under the actual camera.'};
  fs.writeFileSync(path.join(out,'preview-check.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));
});
