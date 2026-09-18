const { chromium } = require('C:/Users/nadom/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const { pathToFileURL } = require('node:url');
const path = require('node:path');
(async()=>{
 const browser=await chromium.launch({headless:true,channel:'msedge'});
 try{
  const page=await browser.newPage({viewport:{width:800,height:600}}), errors=[];
  page.on('pageerror',e=>errors.push(e.message));
  await page.goto(pathToFileURL(path.join(__dirname,'demon-magic-preview-qa.html')).href);
  const f=page.frameLocator('iframe'), root=f.locator('#demon-magic-sprite-preview');
  await root.locator('[id="dm-canvas"]').waitFor();
  await root.locator('#dm-state').filter({hasText:'준비'}).waitFor();
  const results=[];
  for(const [time,orbs,warning] of [[0,0,true],[0.3,1,true],[0.6,2,true],[0.9,3,true],[1.2,4,true],[1.49,5,true],[1.5,4,false]]){
    await f.locator('#dm-seek').evaluate((el,t)=>{el.value=t;el.dispatchEvent(new Event('input',{bubbles:true}));},time);
    const actual=await root.evaluate(el=>({...el.dataset}));
    if(+actual.visibleOrbs!==orbs||actual.warningVisible!==String(warning))throw Error(JSON.stringify({time,actual,orbs,warning}));
    results.push({time,orbs,warning});
  }
  await f.locator('#dm-seek').evaluate(el=>{el.value='1.49';el.dispatchEvent(new Event('input',{bubbles:true}));});
  await page.screenshot({path:path.join(__dirname,'demon-magic-preview-desktop.png')});
  await f.locator('#dm-cancel').click();
  if(await root.getAttribute('data-visible-orbs')!=='0'||await root.getAttribute('data-warning-visible')!=='false')throw Error('Cancel left presentation');
  await f.locator('#dm-play').click();await page.waitForTimeout(2250);
  if(!(await f.locator('#dm-time').innerText()).startsWith('2.10'))throw Error('Playback incomplete');
  await page.setViewportSize({width:360,height:620});
  await f.locator('#dm-seek').evaluate(el=>{el.value='1.49';el.dispatchEvent(new Event('input',{bubbles:true}));});
  await page.screenshot({path:path.join(__dirname,'demon-magic-preview-mobile.png')});
  const overflow=await root.evaluate(el=>document.documentElement.scrollWidth>document.documentElement.clientWidth);
  if(overflow||errors.length)throw Error(JSON.stringify({overflow,errors}));
  console.log(JSON.stringify({passed:true,checkpoints:results,cancel:true,playback:true,mobileOverflow:false,errors}));
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
