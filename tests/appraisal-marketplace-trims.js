const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../Scripts/majordome-appraisal-batch.js'), 'utf8');
const context = vm.createContext({
    URLSearchParams,
    zip: { value: '45011' },
    radius: { value: '100' },
    carGurusEntities: async makeId => makeId
        ? [{ id: 'd2512', name: 'Stelvio' }]
        : [{ id: 'm124', name: 'Alfa Romeo' }]
});

// Run the actual identity and link builders, with an offline CarGurus catalog.
vm.runInContext(source.match(/const vehicleSlug = [^\n]+/)[0], context);
vm.runInContext(source.slice(source.indexOf('    function searchVehicle(row)'), source.indexOf('    function decodedLabel(row)')), context);
vm.runInContext(source.slice(source.indexOf('    function carsDrivetrain(driveType)'), source.indexOf('    function carGurusEntities(makeId)')), context);
vm.runInContext(source.slice(source.indexOf('    async function findCarGurusPaths(row)'), source.indexOf('    function renderActions(row)')), context);

function row(trim = 'Veloce', drive = 'AWD') {
    return {
        Vin: 'ZASPAKBN9P7D56780', Miles: 26124,
        DecodedVehicle: {
            Year: '2023', Make: 'Alfa Romeo', Model: 'Stelvio',
            Style: 'AWD Veloce 4dr Crossover', BodyStyle: 'Wagon', Trim: trim, DriveType: drive
        }
    };
}

(async () => {
    for (const trim of ['Veloce', 'veloce']) {
        const vehicle = row(trim);
        await context.findCarGurusPaths(vehicle);
        const url = new URL(context.carGurusSearchUrl(vehicle));
        assert.equal(url.hostname, 'www.cargurus.com');
        assert.equal(url.searchParams.get('makeModelTrimPaths'), 'm124/d2512/Veloce AWD');
        assert.equal(url.searchParams.get('startYear'), '2023');
        assert.equal(url.searchParams.get('endYear'), '2023');
        assert.equal(url.searchParams.get('zip'), '45011');
        assert.equal(url.searchParams.get('distance'), '100');
        assert.equal(url.searchParams.get('maxMileage'), '26124');
        assert.equal(url.searchParams.get('sortType'), 'PRICE');
        assert.equal(url.searchParams.get('sortDirection'), 'ASC');
        assert.equal(url.searchParams.get('vehicleHistoryOptions'), 'LEMON_FREE,NON_SALVAGE');
        const cars = new URL(context.carsSearchUrl(vehicle));
        assert.equal(cars.hostname, 'www.cars.com');
        assert.equal(cars.searchParams.has('stock_type'), false);
        assert.equal(cars.searchParams.get('clean_title'), 'true');
        assert.equal(cars.searchParams.get('makes[]'), 'alfa_romeo');
        assert.equal(cars.searchParams.get('models[]'), 'alfa_romeo-stelvio');
        assert.equal(cars.searchParams.get('trims[]'), 'alfa_romeo-stelvio-veloce');
        assert.equal(cars.searchParams.has('body_style_slugs[]'), false, 'Do not send body style, including a decoded Wagon classification');
        assert.equal(cars.searchParams.get('drivetrain_slugs[]'), 'all_wheel_drive');
        assert.equal(cars.searchParams.get('year_min'), '2023');
        assert.equal(cars.searchParams.has('year_max'), false);
        assert.equal(cars.searchParams.get('zip'), '45011');
        assert.equal(cars.searchParams.get('maximum_distance'), '100');
        assert.equal(cars.searchParams.get('mileage_max'), '26124');
        assert.equal(cars.searchParams.get('sort'), 'list_price');
    }
    for (const vehicle of [row('', 'AWD'), row('Veloce', ''), row('Veloce', 'RWD')]) {
        await context.findCarGurusPaths(vehicle);
        assert.equal(vehicle.CarGurusPaths, 'm124/d2512', 'Do not guess a trim when decoded values do not support it');
    }
    for (const [decoded, expected] of [['AWD', 'all_wheel_drive'], ['Front-wheel Drive', 'front_wheel_drive'], ['RWD', 'rear_wheel_drive'], ['4WD', 'four_wheel_drive'], ['4x2', null], ['', null]]) {
        assert.equal(context.carsDrivetrain(decoded), expected);
    }
    context.carGurusEntities = async makeId => makeId ? [
        { id: 'd1067', name: 'Transit Cargo' }, { id: 'd2270', name: 'Transit Passenger' },
        { id: 'd3091', name: 'Transit Chassis' }, { id: 'd2037', name: 'Transit Connect' },
        { id: 'd3114', name: 'E-Transit' }
    ] : [{ id: 'm2', name: 'Ford' }];
    for (const [model, style, bodyStyle, path] of [
        ['Transit', 'Extended Length Cargo Van', 'Full-Size', 'm2/d1067'],
        ['Transit', 'Passenger Van', 'Full-Size', 'm2/d2270'],
        ['Transit', 'Chassis Cab', 'Full-Size', 'm2/d3091'],
        ['Transit', 'Extended Length Van', 'Cargo Van', 'm2/d1067'],
        ['Transit', 'Cargo Van', 'Passenger Van', null],
        ['Transit', '', 'Full-Size', null],
        ['Transit 250', 'Cargo Van', 'Full-Size', 'm2/d1067'],
        ['T250', 'Cargo Van', 'Full-Size', 'm2/d1067'],
        ['Transit Connect', 'Passenger Van', 'Full-Size', 'm2/d2037'],
        ['E-Transit', 'Cargo Van', 'Full-Size', 'm2/d3114']
    ]) {
        const vehicle = { Miles: 89675, DecodedVehicle: {
            Year: '2021', Make: 'Ford', Model: model, Trim: '250', Style: style, BodyStyle: bodyStyle, DriveType: 'RWD'
        } };
        await context.findCarGurusPaths(vehicle);
        assert.equal(vehicle.CarGurusPaths || null, path, `Ford ${model}: only an explicit, unambiguous body type selects a Transit subtype`);
        const link = context.carGurusSearchUrl(vehicle);
        if (path) {
            assert.equal(new URL(link).searchParams.get('makeModelTrimPaths'), path);
            assert.equal(new URL(link).searchParams.get('vehicleHistoryOptions'), 'LEMON_FREE,NON_SALVAGE');
        } else {
            assert.equal(link, null, 'Do not guess a CarGurus body type from the 250 trim');
        }
    }
    console.log('PASS: Marketplace trim/history filters, Ford Transit body-type matching, year/mileage/location scope and ambiguity handling.');
})().catch(error => { console.error(error); process.exitCode = 1; });
