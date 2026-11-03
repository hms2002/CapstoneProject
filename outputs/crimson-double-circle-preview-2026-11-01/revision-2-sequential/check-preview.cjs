const fs=require('node:fs'),vm=require('node:vm'),path=require('node:path'),assert=require('node:assert/strict');
const out=__dirname,html=fs.readFileSync(process.argv[2],'utf8');
const source=html.match(/<script>([\s\S]*?)<\/script>/)[1];
const images=['outer','inner'].map(name=>{
  const png=fs.readFileSync(path.join(out,name+'-preview.png'));
  return{naturalWidth:png.readUInt32BE(16),naturalHeight:png.readUInt32BE(20),pixels:new Uint8ClampedArray(fs.readFileSync(path.join(out,name+'.rgba'))),decode:()=>Promise.resolve()};
});
let pixels,callback,input,click;
const ctx={createImageData:(w,h)=>({data:new Uint8ClampedArray(w*h*4)}),putImageData:im=>pixels=Buffer.from(im.data)};
const button={textContent:'',setAttribute:()=>{},addEventListener:(event,fn)=>click=fn};
const slider={value:'35',addEventListener:(event,fn)=>input=fn},value={textContent:''},phase={textContent:''};
const elements={'canvas':{getContext:()=>ctx},'[data-action="pause"]':button,'#crimson-charge-amount':slider,'[data-value="amount"]':value,'[data-value="phase"]':phase};
const root={dataset:{},querySelector:q=>{assert.ok(elements[q],q);return elements[q];},querySelectorAll:()=>images};
const context=vm.createContext({Uint8ClampedArray,Promise,Math,Number,Infinity,
 document:{getElementById:()=>root,createElement:()=>{let image;return{getContext:()=>({drawImage:im=>image=im,getImageData:()=>({data:new Uint8ClampedArray(image.pixels)})})};}},
 window:{addEventListener:()=>{}},matchMedia:()=>({matches:false}),requestAnimationFrame:fn=>callback=fn});
new vm.Script(source).runInContext(context);
setImmediate(()=>{
  assert.equal(root.dataset.ready,'true');
  const frames={};
  for(const p of [0,25,50,75,100]){slider.value=String(p);input();assert.equal(value.textContent,p+'%');frames[p]=Buffer.from(pixels);fs.writeFileSync(path.join(out,'charge-'+p+'.rgba'),pixels);}
  let innerChanged=0,outerChanged=0,outerBefore50=0,innerAfter50=0;
  for(let y=0;y<220;y++)for(let x=0;x<320;x++){
    const radius=Math.hypot(x+.5-160,y+.5-110),i=(y*320+x)*4;
    if(radius<54){
      if(!frames[0].subarray(i,i+3).equals(frames[50].subarray(i,i+3)))innerChanged++;
      if(!frames[50].subarray(i,i+3).equals(frames[100].subarray(i,i+3)))innerAfter50++;
    }
    if(radius>65){
      if(!frames[0].subarray(i,i+3).equals(frames[50].subarray(i,i+3)))outerBefore50++;
      if(!frames[50].subarray(i,i+3).equals(frames[100].subarray(i,i+3)))outerChanged++;
    }
  }
  assert.ok(innerChanged>100);assert.ok(outerChanged>100);assert.equal(outerBefore50,0);assert.equal(innerAfter50,0);
  slider.value='25';input();click();callback(0);callback(40);const advancing=Buffer.from(pixels);callback(80);assert.notDeepEqual(pixels,advancing);
  click();const frozen=Buffer.from(pixels);callback(120);callback(160);assert.deepEqual(pixels,frozen);
  slider.value='50';input();assert.deepEqual(pixels,frames[50]);
  const report={stages:[0,25,50,75,100],outerChangesBefore50:outerBefore50,innerChangesAfter50:innerAfter50,innerChangedPixels:innerChanged,outerChangedPixels:outerChanged,pauseResume:'pass',stationaryGeometry:'pass',rendering:'offline same-JavaScript Canvas pixel adapter',limitations:'Browser DOM/layout and Unity Play Mode not run. Preview only.'};
  fs.writeFileSync(path.join(out,'preview-check.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));
});
