// Offline pixel renderer and interaction check; no browser or network access.
const fs=require('node:fs'),vm=require('node:vm'),path=require('node:path'),assert=require('node:assert/strict');
const out=__dirname;
const html=fs.readFileSync(process.argv[2],'utf8');
const source=html.match(/<script>([\s\S]*?)<\/script>/)[1];
const images=['outer','inner'].map(name=>({naturalWidth:127,naturalHeight:127,pixels:new Uint8ClampedArray(fs.readFileSync(path.join(out,name+'.rgba'))),decode:()=>Promise.resolve()}));
let pixels,callback,click;
const ctx={createImageData:(w,h)=>({data:new Uint8ClampedArray(w*h*4)}),putImageData:im=>pixels=Buffer.from(im.data)};
const button={textContent:'',setAttribute:()=>{},addEventListener:(event,fn)=>click=fn};
const root={dataset:{},querySelector:q=>q==='canvas'?{getContext:()=>ctx}:button,querySelectorAll:()=>images};
const context=vm.createContext({
  Uint8ClampedArray,Promise,Math,Infinity,
  document:{getElementById:()=>root,createElement:()=>{
    let image;return{getContext:()=>({drawImage:im=>image=im,getImageData:()=>({data:new Uint8ClampedArray(image.pixels)})})};
  }},
  window:{addEventListener:()=>{}},
  matchMedia:()=>({matches:false}),requestAnimationFrame:fn=>callback=fn
});
new vm.Script(source).runInContext(context);
setImmediate(()=>{
  assert.equal(root.dataset.ready,'true');assert.ok(pixels);
  const captured=[];
  for(let i=0;i<=160;i++){
    callback(i*1000/30);
    if(i%80===0){fs.writeFileSync(path.join(out,'check-'+i/80+'.rgba'),pixels);captured.push(Buffer.from(pixels));}
  }
  assert.notDeepEqual(captured[0],captured[1]);assert.notDeepEqual(captured[1],captured[2]);
  click();const frozen=Buffer.from(pixels);callback(6000);callback(6100);assert.deepEqual(pixels,frozen);assert.equal(button.textContent,'회전 재생');
  click();callback(6200);callback(6300);assert.notDeepEqual(pixels,frozen);assert.equal(button.textContent,'회전 일시정지');
  const report={renderer:'Same inline JavaScript, offline Canvas pixel buffer adapter',resolution:[320,220],sourceColors:4,checkedAnimationFrames:161,pauseResume:'pass',ready:root.dataset.ready,limitations:'Browser DOM/layout was not executed; local-file browser preview was blocked by URL policy. Unity integration not performed.'};
  fs.writeFileSync(path.join(out,'preview-check.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));
});
