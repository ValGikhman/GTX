;(function ($, window, document) {
    'use strict';
    const form = document.getElementById('pr-batch-inputs');
    if (!form) return;
    const file = document.getElementById('pr-pdf-file'), drop = document.getElementById('pr-pdf-drop');
    const status = document.getElementById('pr-batch-status'), error = document.getElementById('pr-batch-error');
    const run = document.getElementById('pr-batch-run'), stop = document.getElementById('pr-batch-stop');
    const progressModal = document.getElementById('pr-batch-progress-modal');
    const progressMessage = document.getElementById('pr-batch-progress-message');
    const progress = document.getElementById('pr-batch-progress');
    const progressBar = document.getElementById('pr-batch-progress-bar');
    const zip = document.getElementById('pr-batch-zip'), radius = document.getElementById('pr-batch-radius');
    const $grid = $('#pr-batch-grid');
    const money = new Intl.NumberFormat('en-US', {style:'currency',currency:'USD',maximumFractionDigits:0});
    const printMoney = new Intl.NumberFormat('en-US', {style:'currency',currency:'USD'});
    const odometer = new Intl.NumberFormat('en-US', {minimumFractionDigits:2,maximumFractionDigits:2});
    let rows = [], running = false, parsing = false, stopping = false;
    const carGurusCatalog = new Map();
    const vinDecodes = new Map();
    let decodeQueue = Promise.resolve();
    const token = () => form.querySelector('[name="__RequestVerificationToken"]').value;
    const escape = value => $('<span>').text(value == null ? '' : String(value)).html().replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const number = value => typeof value === 'number' && Number.isFinite(value);
    const vehicleSlug = name => (name || '').toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_|_$/g, '');
    function showError(message) { error.textContent = message; error.hidden = !message; }
    function busy() {
        file.disabled = parsing || running; run.disabled = parsing || running || !rows.length;
        stop.hidden = !running; stop.disabled = stopping;
        zip.disabled = radius.disabled = parsing || running;
        drop.classList.toggle('is-busy', parsing || running);
        form.setAttribute('aria-busy', String(parsing || running));
    }
    function searchVehicle(row) {
        const decoded = row.DecodedVehicle;
        if (!decoded) return {};
        const year = String(decoded.Year || '');
        const make = vehicleSlug(decoded.Make);
        let model = vehicleSlug(decoded.Model);
        if (make === 'ford') model = model.replace(/^t_?(150|250|350)(?:_vans?)?$/, 'transit_$1');
        return {year, make, model, trim: decoded.Trim || '', driveType: decoded.DriveType || ''};
    }

    function decodedLabel(row) {
        const decoded = row.DecodedVehicle;
        return decoded ? [decoded.Year, decoded.Make, decoded.Model, decoded.Style, decoded.Trim].filter(Boolean).join(' ') : '';
    }

    function decodeVin(vin) {
        if (vinDecodes.has(vin)) return vinDecodes.get(vin);
        // Keep one decoder request in flight; skip queued VINs removed by a replacement file.
        const request = decodeQueue.then(() => {
            if (!rows.some(row => row.Vin === vin)) throw new Error('Vehicle is no longer in this batch.');
            return $.ajax({url:form.dataset.decodeUrl,type:'POST',dataType:'json',timeout:35000,data:{vin:vin,__RequestVerificationToken:token()}});
        }).then(response => {
            const vehicle = response && response.vehicle;
            if (!vehicle || String(vehicle.Vin).toUpperCase() !== vin || !vehicle.Year || !vehicle.Make || !vehicle.Model)
                throw new Error('The VIN decoder did not return a confirmed vehicle.');
            return vehicle;
        });
        vinDecodes.set(vin, request);
        request.catch(() => { if (vinDecodes.get(vin) === request) vinDecodes.delete(vin); });
        decodeQueue = request.catch(() => {});
        return request;
    }

    async function prepareMarketplaceLinks(row, importedRows) {
        row.DecodeError = null;
        try {
            row.DecodedVehicle = await decodeVin(row.Vin);
            if (rows !== importedRows) return;
            render();
            await findCarGurusPaths(row);
        } catch (failure) {
            row.DecodeError = failure.responseJSON && failure.responseJSON.message || 'VIN details could not be decoded. Retry marketplace links.';
        }
        if (rows === importedRows) render();
    }

    function carsDrivetrain(driveType) {
        const drive = vehicleSlug(driveType).replace(/_/g, '');
        if (drive === 'awd' || drive === 'allwheeldrive') return 'all_wheel_drive';
        if (drive === 'fwd' || drive === 'frontwheeldrive') return 'front_wheel_drive';
        if (drive === 'rwd' || drive === 'rearwheeldrive') return 'rear_wheel_drive';
        if (drive === '4wd' || drive === '4x4' || drive === 'fourwheeldrive') return 'four_wheel_drive';
        return null;
    }

    function validSearchScope() {
        return /^\d{5}$/.test(zip.value) && /^[1-9]\d{0,2}$/.test(radius.value) && Number(radius.value) <= 500;
    }

    function validSearchYear(year) {
        return /^[0-9]{4}$/.test(year) && Number(year) >= 1886 && Number(year) <= 2100;
    }

    function validMileage(miles) {
        return Number.isInteger(miles) && miles >= 0 && miles <= 2000000;
    }

    function carsSearchUrl(row) {
        const {year, make, model, trim, driveType} = searchVehicle(row);
        if (!validSearchYear(year) || !make || !model || !validSearchScope()) return null;

        const query = new URLSearchParams({
            year_min: year,
            clean_title: 'true',
            'makes[]': make,
            'models[]': make + '-' + model,
            zip: zip.value,
            maximum_distance: radius.value,
            sort: 'list_price'
        });
        if (trim) query.set('trims[]', make + '-' + model + '-' + vehicleSlug(trim));
        const drivetrain = carsDrivetrain(driveType);
        if (drivetrain) query.set('drivetrain_slugs[]', drivetrain);
        if (validMileage(row.Miles)) {
            query.set('mileage_max', row.Miles);
        }
        return 'https://www.cars.com/shopping/results/?' + query.toString();
    }

    function carGurusEntities(makeId) {
        const path = makeId ? 'listModels.action' : 'listMakes.action';
        if (!carGurusCatalog.has(makeId)) {
            const query = new URLSearchParams({searchType: 'USED'});
            if (makeId) query.set('makeId', makeId);
            const request = Promise.resolve($.ajax({
                url: 'https://www.cargurus.com/Cars/api/1.0/carselector/' + path + '?' + query,
                dataType: 'json',
                timeout: 6000
            })).then(response => {
                const entities = makeId ? response.models : response.makes;
                if (!response.success || !Array.isArray(entities)) throw new Error('CarGurus catalog unavailable.');
                return entities;
            }).catch(error => {
                carGurusCatalog.delete(makeId);
                throw error;
            });
            carGurusCatalog.set(makeId, request);
        }
        return carGurusCatalog.get(makeId);
    }

    async function findCarGurusPaths(row) {
        const vehicle = searchVehicle(row);
        if (!validSearchYear(vehicle.year) || !vehicle.make || !vehicle.model) return;
        const normalize = name => vehicleSlug(name).replace(/_/g, '');
        try {
            const makes = await carGurusEntities();
            const make = makes.find(item => normalize(item.name) === normalize(vehicle.make));
            if (!make || !/^m\d+$/.test(make.id)) return;

            let modelName = vehicle.model;
            if (vehicle.make === 'ford' && /^transit_?(150|250|350)$/.test(modelName)) modelName = 'transit_cargo';
            if (vehicle.make === 'ford' && modelName === 'transit') {
                // Some VINs decode as Transit; CarGurus splits it by body type.
                const style = vehicleSlug([row.DecodedVehicle.Style, row.DecodedVehicle.BodyStyle].filter(Boolean).join(' '));
                const bodies = ['cargo', 'passenger', 'chassis'].filter(body => new RegExp('(^|_)' + body + '(_|$)').test(style));
                if (bodies.length === 1) modelName = 'transit_' + bodies[0];
            }
            if (vehicle.make === 'lexus' && /^es_?300h(?:_|$)/.test(modelName)) modelName = 'es_hybrid';
            const es350 = vehicle.make === 'lexus' && /^es_?350(?:_|$)/.test(modelName);
            if (es350) modelName = 'es';
            const modelKey = normalize(modelName);
            const models = await carGurusEntities(make.id);
            let model, prefixLength = 0;
            for (const item of models) {
                if (!/^d\d+$/.test(item.id)) continue;
                const slug = vehicleSlug(item.name);
                if (modelKey === slug.replace(/_/g, '')) {
                    model = item;
                    break;
                }
                if (slug.length > prefixLength && modelName.startsWith(slug + '_')) {
                    model = item;
                    prefixLength = slug.length;
                }
            }
            if (!model) return;

            const path = make.id + '/' + model.id;
            const es350Trims = ['Premium FWD', 'F Sport Design FWD', 'Ultra Luxury FWD', 'FWD',
                'Luxury FWD', 'Crafted Line FWD', 'F Sport Handling FWD', 'F Sport FWD'];
            if (path === 'm124/d2512' && normalize(vehicle.trim) === 'veloce' && normalize(vehicle.driveType) === 'awd') {
                row.CarGurusPaths = path + '/Veloce AWD';
            } else if (path === 'm37/d2720' && es350) {
                // CarGurus groups ES 350 under ES; only use its known trim labels.
                const normalizeTrim = name => vehicleSlug(name).replace(/_/g, '');
                const trimKey = normalizeTrim(vehicle.trim);
                const drive = /^(FWD|RWD|AWD|4WD)$/i.test(vehicle.driveType) ? vehicle.driveType.toUpperCase() : '';
                const candidates = es350Trims.filter(trim => !trimKey || normalizeTrim(trim.replace(/ (FWD|RWD|AWD|4WD)$/, '')) === trimKey)
                    .filter(trim => !drive || trim.endsWith(' ' + drive));
                row.CarGurusPaths = candidates.length ? candidates.map(trim => path + '/350 ' + trim).join(',') : path;
            } else {
                row.CarGurusPaths = path;
            }
        } catch {
            // The batch remains usable if CarGurus is temporarily unavailable.
        }
    }

    function carGurusSearchUrl(row) {
        if (!row.CarGurusPaths || !validSearchScope()) return null;
        const {year} = searchVehicle(row);
        const query = new URLSearchParams({
            sourceContext: 'carGurusHomePageModel',
            zip: zip.value,
            endYear: year,
            hideNationwideShipping: 'true',
            isDeliveryEnabled: 'true',
            makeModelTrimPaths: row.CarGurusPaths,
            priceDropsOnly: 'false',
            startYear: year,
            distance: radius.value,
            nonShippableBaseline: '4',
            sortDirection: 'ASC',
            sortType: 'PRICE',
            vehicleHistoryOptions: 'LEMON_FREE,NON_SALVAGE'
        });
        if (validMileage(row.Miles)) query.set('maxMileage', row.Miles);
        return 'https://www.cargurus.com/search?' + query;
    }

    function renderActions(row) {
        const url = carsSearchUrl(row);
        const cars = url ? `<a class="btn btn-sm pr-batch-retry" href="${escape(url)}" target="_blank" rel="noopener noreferrer" title="${escape(decodedLabel(row))}" aria-label="Search Cars.com (opens in a new tab)"><img class="pr-batch-cars-logo" src="https://www.cars.com/images/cars_logo_primary@2x-53820ca3431bcb460d83320fcd9e12f2.png?vsn=d" alt="Cars.com" referrerpolicy="no-referrer"></a>` : '';
        const gurusUrl = carGurusSearchUrl(row);
        const gurus = gurusUrl ? `<a class="btn btn-sm pr-batch-retry" href="${escape(gurusUrl)}" target="_blank" rel="noopener noreferrer" title="${escape(decodedLabel(row))}" aria-label="Search CarGurus (opens in a new tab)"><img class="pr-batch-cars-logo" src="${form.dataset.cargurusLogo}" alt="CarGurus"></a>` : '';
        const decoding = row.DecodedVehicle ? '' : row.DecodeError
            ? `<button type="button" class="btn btn-sm pr-batch-retry" data-marketplace-vin="${escape(row.Vin)}" title="${escape(row.DecodeError)}" aria-label="Retry VIN decoding for marketplace searches"><i class="bi bi-arrow-clockwise" aria-hidden="true"></i></button>`
            : '<span class="small text-muted align-self-center" role="status" title="Decoding VIN for marketplace links"><span class="spinner-border spinner-border-sm" aria-hidden="true"></span><span class="visually-hidden">Decoding VIN</span></span>';
        const retry = `<button type="button" class="btn btn-sm pr-batch-retry" aria-label="Retry MarketCheck appraisal" title="Retry MarketCheck appraisal" data-vin="${escape(row.Vin)}" ${running || parsing ? 'disabled' : ''}><img class="pr-batch-cars-logo pr-batch-marketcheck-logo" src="https://www.marketcheck.com/images/logo/mc-logo-og-light.svg" alt="MarketCheck" referrerpolicy="no-referrer"></button>`;
        return `<div class="d-flex flex-wrap justify-content-start gap-1">${retry}${cars}${gurus}${decoding}</div>`;
    }

    function render() {
        if ($grid.data('add-grid')) { $grid.addGrid('setData', rows, {preserveState:true}); return; }
        $grid.addGrid({data:rows, columns:[
            {field:'Run',title:'LANE',width:'8%',filterable:false},
            {field:'Vehicle',title:'Vehicle',width:'16%',filterable:false},
            {field:'Vin',title:'VIN',exportValue:row=>row.Vin,width:'13%',filterable:false},
            {field:'Miles',exportAlign:'right',title:'Miles',exportValue:(row,format)=>format==='pdf'&&number(row.Miles)?odometer.format(row.Miles):row.Miles,width:'9%',filterable:false,searchable:false,headerClass:'text-end',cellClass:'text-end',template:row => '<span class="pr-batch-odometer">'+(number(row.Miles)?new Intl.NumberFormat('en-US',{maximumFractionDigits:0}).format(row.Miles):'—')+'</span>'},
            {field:'Estimate',exportAlign:'right',title:'Estimate',exportValue:(row,format)=>format==='pdf'&&number(row.Estimate)?printMoney.format(row.Estimate):row.Estimate,width:'11%',filterable:false,searchable:false,headerClass:'text-end',cellClass:'text-end',template:row => number(row.Estimate)?money.format(row.Estimate):'—'},
            {field:'Maximum',exportAlign:'right',title:'Max bid',exportValue:(row,format)=>number(row.Maximum)?(format==='pdf'?printMoney.format(row.Maximum):row.Maximum):row.Done?'No feasible bid':null,width:'9%',filterable:false,searchable:false,headerClass:'text-end',cellClass:'text-end',template:row => number(row.Maximum)?money.format(row.Maximum):row.Done?'No feasible bid':'—'},
            {field:'Status',title:'Status',exportValue:row=>row.Status,width:'12%',filterable:false,headerClass:'text-center',cellClass:'text-center',template:row =>
                `<span class="badge text-bg-${row.StatusTone} mw-100 text-wrap text-break text-center px-3 py-2">${escape(row.Status)}</span>`},
            {field:'Actions',title:'Actions',width:'22%',headerClass:'text-center',cellClass:'text-center',sortable:false,filterable:false,searchable:false,template:renderActions}
        ],tableClass:'table align-middle m-0',height:null,pageable:false,resizable:false,reorderable:false,sortable:true,searchable:true,showSearch:true,searchPlaceholder:'Search run, vehicle, VIN, or status…',showRecordCount:true,recordType:{singular:'vehicle',plural:'vehicles',icon:'bi bi-car-front'},exportToExcel:false,exportToPdf:false,showPrint:true,exportFileName:'Auction shortlist',exportPdfWrap:true,groupable:false,alternateRows:true});
    }
    async function appraise(row) {
        if (!number(row.Miles) || !Number.isInteger(row.Miles) || row.Miles < 0 || row.Miles > 2000000) {
            row.Status = 'Mileage missing or invalid in file; skipped';
            row.StatusTone = 'danger';
            return;
        }
        row.Done=false; row.Estimate=row.Maximum=null;
        row.StatusTone = 'info';
        row.Status='Researching MarketCheck…'; render();
        try {
            const result = await $.ajax({url:form.dataset.appraiseUrl,type:'POST',dataType:'json',timeout:90000,data:{vin:row.Vin,miles:row.Miles,zip:zip.value,radius:radius.value,__RequestVerificationToken:token()}});
            if (!number(result.marketCheckRetailUsd)) throw new Error('No usable MarketCheck estimate returned.');
            row.Estimate=result.marketCheckRetailUsd; row.Maximum=result.maximumHammerBidUsd; row.Done=true;
            row.Status=result.message || 'Complete';
            row.StatusTone = number(row.Maximum) ? 'success' : 'info';
        } catch (failure) {
            const body=failure.responseJSON;
            if (body && number(body.marketCheckRetailUsd)) row.Estimate=body.marketCheckRetailUsd;
            row.Status=body && body.message || failure.message ||
                (failure.statusText === 'timeout' ? 'The appraisal request timed out. Retry this vehicle.' :
                    (failure.status ? 'Appraisal failed (HTTP ' + failure.status + '). Retry this vehicle.' : 'The appraisal connection did not return a response.'));
            row.StatusTone = 'danger';
            if ([401,403,429].includes(failure.status)) { stopping=true; showError(row.Status); }
        }
        render();
    }
    async function start(selected) {
        if (running || parsing || !form.reportValidity()) return;
        const queue = selected || rows.filter(row => !row.Done);
        if (!queue.length) return;
        const modal = window.bootstrap.Modal.getOrCreateInstance(progressModal);
        const previousFocus = document.activeElement;
        running=true; stopping=false; showError(''); busy(); render();
        try {
            status.textContent = '';
            progress.setAttribute('aria-valuenow', '0');
            progressBar.style.width = '0%';
            progressMessage.textContent = 'Starting appraisal…';
            modal.show();
            for (let i=0;i<queue.length && !stopping;i++) {
                progressMessage.textContent = `Appraising ${i + 1} of ${queue.length} · ${queue[i].Vehicle} · ${queue[i].Vin}`;
                await appraise(queue[i]);
                const percent = Math.round((i + 1) * 100 / queue.length);
                progress.setAttribute('aria-valuenow', String(percent));
                progressBar.style.width = `${percent}%`;
            }
        } finally {
            running = false;
            modal.hide();
            busy();
            render();
            const focusTarget = previousFocus && previousFocus.isConnected ? previousFocus : run;
            focusTarget.focus();
        }
    }
    async function upload(selected) {
        if (!selected || running || parsing) return;
        if (!/\.(pdf|csv)$/i.test(selected.name) || selected.size > 10*1024*1024) { showError('Select a PDF or CSV file up to 10 MB.'); return; }
        if (!form.reportValidity()) return;
        parsing=true; showError(''); busy(); status.textContent='Reading '+selected.name+'…';
        try {
            const data=new FormData();data.append('file',selected);data.append('__RequestVerificationToken',token());
            const parsed=await $.ajax({url:form.dataset.parseUrl,type:'POST',data,processData:false,contentType:false,dataType:'json',timeout:45000});
            if (!Array.isArray(parsed.vehicles) || !parsed.vehicles.length) throw new Error('No vehicles were found in this file.');
            rows=parsed.vehicles.map(row=>({Run:row.Run,Vin:row.Vin,Vehicle:row.Vehicle||'Vehicle · PDF page '+row.Page,Year:row.Year,Make:row.Make,Model:row.Model,Miles:row.Miles,Estimate:null,Maximum:null,Done:false,Status:row.Warning||'Ready',StatusTone:row.Warning?'danger':'info'}));
            parsing = false;
            render(); status.textContent='';
            document.getElementById('pr-pdf-drop-title').textContent = selected.name;
            document.getElementById('pr-pdf-drop-summary').textContent =
                rows.length === 1 ? '1 vehicle imported' : `${rows.length} vehicles imported`;
            document.getElementById('pr-pdf-drop-badge').hidden = false;
            document.getElementById('pr-pdf-drop-hint').textContent = 'Drop another file or click to replace';
            document.getElementById('pr-pdf-drop-icon').className = 'bi bi-file-earmark-check';
            drop.classList.add('is-loaded');
            const importedRows = rows;
            importedRows.forEach(row => prepareMarketplaceLinks(row, importedRows));
        } catch (failure) { showError(failure.responseJSON && failure.responseJSON.message || failure.message || 'Could not read this file.'); }
        finally { parsing=false;file.value='';busy(); }

    }
    form.addEventListener('submit',event=>{event.preventDefault();return start();});
    file.addEventListener('change',()=>upload(file.files[0]));
    ['dragenter','dragover'].forEach(name=>drop.addEventListener(name,event=>{event.preventDefault();if(!running&&!parsing)drop.classList.add('is-dragging');}));
    ['dragleave','drop'].forEach(name=>drop.addEventListener(name,event=>{event.preventDefault();drop.classList.remove('is-dragging');}));
    drop.addEventListener('drop',event=>upload(event.dataTransfer.files[0]));
    stop.addEventListener('click',()=>{stopping=true;busy();progressMessage.textContent='Stopping after the current vehicle…';});
    $grid.on('click','button.pr-batch-retry',function(){const row=rows.find(row=>row.Vin===this.dataset.vin);if(row)start([row]);});
    $grid.on('click','button[data-marketplace-vin]',function(){const row=rows.find(row=>row.Vin===this.dataset.marketplaceVin);if(row)prepareMarketplaceLinks(row, rows);});
    [zip,radius].forEach(input=>input.addEventListener('input',()=>{if(running||parsing)return;rows.forEach(row=>{row.Done=false;row.Estimate=row.Maximum=null;row.Status='Ready';row.StatusTone='info';});if(rows.length)render();}));
    document.querySelectorAll('[data-appraisal-mode]').forEach(button=>button.addEventListener('click',()=>{
        const multiple=button.dataset.appraisalMode==='multiple';
        document.getElementById('pr-individual-inputs').hidden=multiple;form.hidden=!multiple;
        document.querySelectorAll('[data-appraisal-scope]').forEach(scope=>{scope.hidden=scope.dataset.appraisalScope!==button.dataset.appraisalMode;});
        document.getElementById('pr-individual-results').hidden=multiple;document.getElementById('pr-batch-results').hidden=!multiple;
        document.querySelectorAll('[data-appraisal-mode]').forEach(tab=>{const selected=tab===button;tab.classList.toggle('active',selected);tab.setAttribute('aria-selected',String(selected));});
        if(multiple&&rows.length)render();
    }));
    window.addEventListener('beforeunload',event=>{if(running){event.preventDefault();event.returnValue='';}});
}(window.jQuery,window,document));
