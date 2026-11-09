const assert = require('node:assert/strict'), fs = require('node:fs'), vm = require('node:vm');
let lastData;
const widget = { active:false, data(){return this.active;}, addGrid(arg){ if(arg==='destroy'){this.active=false;return this;} if(typeof arg==='object'){this.active=true;lastData=arg.data;} return this; }, find(){return {attr(){}};} };
function $(value, attrs){ if(typeof value==='string') return [{outerHTML: attrs && attrs.text || ''}]; return widget; }
const window = {jQuery:$}; vm.runInNewContext(fs.readFileSync(require('node:path').join(__dirname,'../Scripts/majordome-appraisal-comparables.js'),'utf8'), {window, URL, Intl, document:{createElement(){return {};}}});
const listeners = {};
const current = {checked:true,addEventListener(_,cb){listeners.current=cb;}};
const recent = {checked:false,addEventListener(_,cb){listeners.recent=cb;}};
const nodes={ '[data-appraisal-grid]':{setAttribute(){}}, '[data-appraisal-caption]':{},'[data-appraisal-description]':{}, '[data-appraisal-note]':{appendChild(){}},'[data-appraisal-period][value="current"]':current,'[data-appraisal-period][value="recent"]':recent };
const section={querySelector(s){return nodes[s];},getAttribute(){return 'heading';}};
const grid=window.majordomeAppraisalComparables.create(section);
const scope={Vin:'SUBJECT',Miles:50000,Radius:100};
const listing=(vin,price)=>({vin,price,miles:50000,dist:20});
const data={comparables:{listings:[listing('MC1',20000)]},recent_comparables:{listings:[listing('MC2',19000)]}};
grid.show(data,scope); assert.equal(lastData.map(x=>x.Vin).join(','),'MC1');
current.checked=false; recent.checked=true; listeners.recent();
assert.equal(lastData.map(x=>x.Vin).join(','),'MC2');
grid.show(data,scope); assert.equal(current.checked,true); assert.equal(lastData[0].Vin,'MC1');
console.log('PASS: MarketCheck current and recent comparables and reset on new research.');
