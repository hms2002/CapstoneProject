const fs=require('node:fs'),vm=require('node:vm'),path=require('node:path'),assert=require('node:assert/strict');
const out=__dirname,art=path.join(out,'../revision-2-sequential'),html=fs.readFileSync(process.argv[2],'utf8');
const source=html.match(/<script>([\s\S]*?)<\/script>/)[1];
assert.ok(!source.includes('Math.atan2'),'No angular fill mask');
assert.ok(Buffer.byteLength(html)<1000000,'Fragment size');
const images=['outer','inner'].map(name=>{
  const png=fs.readFileSync(path.join(art,name+'-preview.png'));
  assert.ok(html.includes(png.toString('base64')),'Embedded artwork matches checked pixel source');
  return{naturalWidth:png.readUInt32BE(16),naturalHeight:png.readUInt32BE(20),pixels:new Uint8ClampedArray(fs.readFileSync(path.join(art,name+'.rgba'))),decode:()=>Promise.resolve()};
});
let pixels,callback,input,click;
const ctx={createImageData:(w,h)=>({data:new Uint8ClampedArray(w*h*4)}),putImageData:im=>pixels=Buffer.from(im.data)};
const button={textContent:'',setAttribute:()=>{},addEventListener:(event,fn)=>click=fn};
const slider={value:'35',addEventListener:(event,fn)=>input=fn},value={textContent:''},phase={textContent:''};
const elements={'canvas':{getContext:()=>ctx},'[data-action="pause"]':button,'#crimson-scale-amount':slider,'[data-value="amount"]':value,'[data-value="phase"]':phase};
const root={dataset:{},querySelector:q=>{assert.ok(elements[q],q);return elements[q];},querySelectorAll:()=>images};
const context=vm.createContext({Uint8ClampedArray,Promise,Math,Number,Infinity,
 document:{getElementById:id=>{assert.ok(html.includes('id="'+id+'"'));return root;},createElement:()=>{let image;return{getContext:()=>({drawImage:im=>image=im,getImageData:()=>({data:new Uint8ClampedArray(image.pixels)})})};}},
 window:{addEventListener:()=>{}},matchMedia:()=>({matches:false}),requestAnimationFrame:fn=>callback=fn});
new vm.Script(source).runInContext(context);
setImmediate(()=>{
  assert.equal(root.dataset.ready,'true');
  const stages=[0,25,50,75,100],radii=[],frames={};
  const expectedInner=[0,28.6,57.2,57.2,57.2],expectedOuter=[0,0,0,72.6,88];
  for(let n=0;n<stages.length;n++){
    const p=stages[n];slider.value=String(p);input();assert.equal(value.textContent,p+'%');
    const inner=Number(root.dataset.innerRadius),outer=Number(root.dataset.outerRadius);
    assert.ok(Math.abs(inner-expectedInner[n])<1e-8);assert.ok(Math.abs(outer-expectedOuter[n])<1e-8);
    radii.push({charge:p,inner,outer});frames[p]=Buffer.from(pixels);fs.writeFileSync(path.join(out,'charge-'+p+'.rgba'),pixels);
    const limit=Math.max(inner,outer)+1;
    for(let y=0;y<220;y++)for(let x=0;x<320;x++){
      const i=(y*320+x)*4;
      if(Math.hypot(x+.5-160,y+.5-110)>limit)assert.ok(pixels.subarray(i,i+4).equals(frames[0].subarray(i,i+4)),'No pixels beyond scaled boundary');
    }
  }
  function brightNear(frame,x,y){
    for(let yy=Math.floor(y)-1;yy<=Math.ceil(y)+1;yy++)for(let xx=Math.floor(x)-1;xx<=Math.ceil(x)+1;xx++){
      const i=(yy*320+xx)*4;if(frame[i]>190&&frame[i+1]>140)return true;
    }
    return false;
  }
  for(const [p,r] of [[25,28.6],[75,72.6]])for(const [dx,dy] of [[r,0],[0,r],[-r,0],[0,-r]])
    assert.ok(brightNear(frames[p],159.5+dx,109.5+dy),'Full circumference at '+p+'%, no radial clipping');
  slider.value='75';input();const before={outer:Number(root.dataset.outerAngle),inner:Number(root.dataset.innerAngle)};
  click();callback(0);callback(40);const advancing=Buffer.from(pixels);callback(80);
  assert.notDeepEqual(pixels,advancing);assert.ok(Number(root.dataset.outerAngle)>before.outer);assert.ok(Number(root.dataset.innerAngle)<before.inner);
  click();const frozen=Buffer.from(pixels),angles=[root.dataset.innerAngle,root.dataset.outerAngle];
  callback(120);callback(160);assert.deepEqual(pixels,frozen);assert.deepEqual([root.dataset.innerAngle,root.dataset.outerAngle],angles);
  click();callback(200);assert.notDeepEqual(pixels,frozen);
  const report={radii,fullCircumferenceAtPartialCharge:'pass',scaledBoundary:'pass',oppositePlanarRotation:'pass',pauseResume:'pass',artwork:'unchanged revision 2',rendering:'offline same-JavaScript Canvas pixel adapter',limitations:'Browser DOM/layout and Unity Play Mode not run. Preview only.'};
  fs.writeFileSync(path.join(out,'preview-check.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));
});
