const fs=require('node:fs'),vm=require('node:vm'),path=require('node:path'),assert=require('node:assert/strict');
const out=__dirname,html=fs.readFileSync(process.argv[2],'utf8');
const W=192,H=144,CX=96,CY=72;
const source=html.match(/<script>([\s\S]*?)<\/script>/)[1];
assert.ok(!source.includes('Math.atan2'),'No angular fill mask');
assert.ok(Buffer.byteLength(html)<1000000,'Fragment size');
const images=['outer','inner'].map(name=>{
  const png=fs.readFileSync(path.join(out,name+'-preview.png'));
  const size=name==='inner'?64:96;
  assert.equal(png.readUInt32BE(16),size);assert.equal(png.readUInt32BE(20),size);
  assert.ok(html.includes(png.toString('base64')),'Embedded artwork matches checked pixel source');
  const bytes=new Uint8ClampedArray(fs.readFileSync(path.join(out,name+'.rgba')));
  for(let i=0;i<bytes.length;i+=4){assert.ok(bytes[i+3]===0||bytes[i+3]===255);if(bytes[i+3])assert.ok(['255,198,90','241,107,50','255,240,191'].includes(Array.from(bytes.slice(i,i+3)).join(',')));}
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
  const expectedInner=[0,16,32,32,32],expectedOuter=[0,0,0,40,48];
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
  for(const [p,r] of [[25,16],[75,40]])for(const [dx,dy] of [[r,0],[0,r],[-r,0],[0,-r]])
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
  for(let y=0;y<H;y++)for(let x=0;x<W;x++)if(Math.hypot(x+.5-CX,y+.5-CY)<34){const i=(y*W+x)*4;assert.ok(pixels[i]<190,'Separate outer ring has no inner artwork');}
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
  const report={radii,nativePixelSizes:{inner:64,outer:96,ppu:16,maximumDiameter:6},nativeSourcePixelMapping:'pass',palette:'FFC65A F16B32 FFF0BF from existing six-color palette; binary alpha',wholeInnerCompletedAt50:'pass',separateLayers:'pass',innerOpaquePixels:innerCount,outerOpaquePixels:outerCount,fullCircumferenceAtPartialCharge:'pass',scaledBoundary:'pass',oppositePlanarRotation:'pass',pauseResume:'pass',artwork:'Native Aseprite pixel geometry; complete inner sigil and independent outer ring',rendering:'offline same-JavaScript Canvas pixel adapter',limitations:'Browser DOM/layout and Unity Play Mode not run. Pixel density matches the 16 PPU Crimson family at nominal full size; scaled particles and rotation can vary on screen.'};
  fs.writeFileSync(path.join(out,'preview-check.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));
});
