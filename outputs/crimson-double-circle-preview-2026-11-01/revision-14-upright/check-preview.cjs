const fs=require('node:fs'),vm=require('node:vm'),path=require('node:path'),assert=require('node:assert/strict');
const out=__dirname,html=fs.readFileSync(process.argv[2],'utf8');
const W=240,H=180,CX=120,CY=90;
const source=html.match(/<script>([\s\S]*?)<\/script>/)[1];
const paletteCounts={},ringChecks=[];
assert.ok(!source.includes('Math.atan2'),'No angular fill mask');
assert.ok(Buffer.byteLength(html)<1000000,'Fragment size');
const images=['outer-rings','inner-rings','inner-star','glyph-atlas','medallion'].map(name=>{
  const png=fs.readFileSync(path.join(out,name+'.png'));
  const w=png.readUInt32BE(16),h=png.readUInt32BE(20);
  assert.ok(html.includes(png.toString('base64')),'Embedded artwork matches checked pixel source');
  const bytes=new Uint8ClampedArray(fs.readFileSync(path.join(out,name+'.rgba')));
  const palette=new Map([['255,198,90',0],['255,240,191',0],['182,61,40',0]]);
  for(let i=0;i<bytes.length;i+=4){
    assert.ok(bytes[i+3]===0||bytes[i+3]===255);
    if(bytes[i+3]){const rgb=Array.from(bytes.slice(i,i+3)).join(',');assert.ok(palette.has(rgb),'Exactly the three authored colors');palette.set(rgb,palette.get(rgb)+1);}
  }
  for(const count of palette.values())assert.ok(count>0,'All three colors used');
  paletteCounts[name]=Object.fromEntries(palette);
  return{naturalWidth:w,naturalHeight:h,pixels:bytes,decode:()=>Promise.resolve()};
});
// Isolate each ring in a padded image. Exactly two empty regions means a closed
// central opening and exterior, without a one-pixel pinhole in the band.
for(const [index,radii] of [[0,[59.5,47.5]],[1,[39.5,27.5,16.5,9.5]],[4,[4.5]]]){
  const tex=images[index],n=tex.naturalWidth,s=n+4,c=(n-1)/2;
  for(const r of radii){
    const blocked=new Uint8Array(s*s),seen=new Uint8Array(s*s),regions=[];
    for(let y=0;y<n;y++)for(let x=0;x<n;x++){
      const distance=Math.hypot(x-c,y-c);
      if(distance>=r-1.5&&distance<=r+.5&&tex.pixels[(y*n+x)*4+3])blocked[(y+2)*s+x+2]=1;
    }
    for(let start=0;start<s*s;start++)if(!blocked[start]&&!seen[start]){
      let total=0;const queue=[start];seen[start]=1;
      for(let q=0;q<queue.length;q++){
        const p=queue[q],x=p%s,y=Math.floor(p/s);total++;
        for(const [dx,dy] of [[1,0],[-1,0],[0,1],[0,-1]]){
          const nx=x+dx,ny=y+dy,j=ny*s+nx;
          if(nx>=0&&ny>=0&&nx<s&&ny<s&&!blocked[j]&&!seen[j]){seen[j]=1;queue.push(j);}
        }
      }
      regions.push(total);
    }
    assert.equal(regions.length,2,'Closed ring without trapped pinholes at radius '+r);
    assert.ok(regions.every(size=>size>1));ringChecks.push({radius:r,emptyRegions:regions.length});
  }
}
const completeInner=fs.readFileSync(path.join(out,'inner.rgba'));
const completeOuter=fs.readFileSync(path.join(out,'outer.rgba'));
function assertUprightOuter(frame,angle){
  const tex=images[3],w=tex.naturalWidth;
  for(let k=0;k<16;k++){
    const a=-Math.PI/2+k*Math.PI/8+angle;
    const left=Math.round(CX-.5+53*Math.cos(a)-3),top=Math.round(CY-.5+53*Math.sin(a)-3);
    for(let y=0;y<7;y++)for(let x=0;x<7;x++){
      const j=((y+1)*w+(k%6)*9+1+x)*4,i=((top+y)*W+left+x)*4;
      if(tex.pixels[j+3])assert.deepEqual(Array.from(frame.slice(i,i+3)),Array.from(tex.pixels.slice(j,j+3)),'Upright glyph keeps exact authored pixel colors');
    }
  }
}
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
      const i=(yy*W+xx)*4;if(frame[i]>150&&frame[i+1]>40)return true;
    }
    return false;
  }
  for(const [p,r] of [[25,20],[75,50]])for(const [dx,dy] of [[r,0],[0,r],[-r,0],[0,-r]])
    assert.ok(brightNear(frames[p],CX-.5+dx,CY-.5+dy),'Full circumference at '+p+'%, no radial clipping');
  function brightMask(frame){const mask=[];for(let i=0;i<frame.length;i+=4)mask.push(frame[i]>150&&frame[i+1]>40);return mask;}
  layerPicker.value='inner';changeLayer();assert.equal(root.dataset.layer,'inner');
  assert.deepEqual(brightMask(pixels),brightMask(frames[50]),'Entire inner design is complete at 50%');
  fs.writeFileSync(path.join(out,'layer-inner.rgba'),pixels);
  const innerCount=brightMask(pixels).filter(Boolean).length;
  assert.equal(innerCount,Array.from(completeInner).filter((v,i)=>i%4===3&&v===255).length,'Inner displayed one logical pixel per source pixel');
  layerPicker.value='outer';changeLayer();assert.equal(root.dataset.layer,'outer');
  fs.writeFileSync(path.join(out,'layer-outer.rgba'),pixels);
  const outerCount=brightMask(pixels).filter(Boolean).length;
  assert.equal(outerCount,Array.from(completeOuter).filter((v,i)=>i%4===3&&v===255).length,'Outer displayed one logical pixel per source pixel');
  assertUprightOuter(pixels,0);
  for(let y=0;y<H;y++)for(let x=0;x<W;x++)if(Math.hypot(x+.5-CX,y+.5-CY)<42.5){const i=(y*W+x)*4;assert.ok(pixels[i]<150,'Separate outer ring has no inner artwork');}
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
  assert.equal(root.dataset.glyphAngle,'0');assert.equal(root.dataset.charge,'1');
  assertUprightOuter(pixels,Number(root.dataset.outerAngle));
  const meta=fs.readFileSync(path.join(process.cwd(),'Assets/_Project/Art/Sprites/VFX/CrimsonBoundary/ChargeSixColor/Frames/impact_core_01.png.meta'),'utf8');
  const charge=fs.readFileSync(path.join(process.cwd(),'Assets/_Project/Data/Items/Weapons/LogicData/ALData_CrimsonBoundaryCharge.asset'),'utf8');
  assert.match(meta,/spritePixelsToUnits: 16/);assert.match(meta,/filterMode: 0/);assert.match(charge,/maximumDiameter: 6/);
  const report={radii,nativePixelSizes:{inner:80,outer:120,glyphAtlas:[126,9],proposedPpu:20,existingCrimsonPpu:16,maximumDiameter:6},palette:'FFC65A / FFF0BF / B63D28; binary alpha',paletteCounts,ringChecks,continuousBorders:'Seven isolated ring masks each have exactly two empty regions: center and exterior, no pinholes',regularRunes:'7x7 rectilinear upright; 5x5 small marks; outer 16',uprightGlyphs:'Exact atlas colors/positions checked at rest and while orbiting; rotation 0',nativeSourcePixelMapping:'pass',wholeInnerCompletedAt50:'pass',separateLayers:'pass',innerOpaquePixels:innerCount,outerOpaquePixels:outerCount,fullCircumferenceAtPartialCharge:'pass',scaledBoundary:'pass',oppositeOrbits:'pass',pauseResume:'pass',rendering:'offline same-JavaScript Canvas pixel adapter',limitations:'Browser DOM/layout and Unity import/Play Mode not run. Proposed 20 PPU remains unapplied. Runtime integration must use split art and keep glyph orientation upright; flattening and rotating the complete PNG would lose this behavior. Small-scale and actual-camera sampling remain integration acceptance items.'};
  fs.writeFileSync(path.join(out,'preview-check.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));
});
