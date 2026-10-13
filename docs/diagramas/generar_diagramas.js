// Generador de FlowRide_Modelo.drawio (3 páginas: Dominio, Motor de asignación, Entidad-Relación)
const fs = require('fs');
const OUT = process.argv[2];

const xmlEsc = s => s.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
const h = s => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

const PAL = {
  rf10:   { fill: '#FFE6CC', stroke: '#D79B00' },
  dom:    { fill: '#DAE8FC', stroke: '#6C8EBF' },
  v1:     { fill: '#D5E8D4', stroke: '#82B366' },
  trans:  { fill: '#F5F5F5', stroke: '#666666' },
  enum:   { fill: '#E1D5E7', stroke: '#9673A6' },
  vo:     { fill: '#FFF2CC', stroke: '#D6B656' },
  port:   { fill: '#FFFFFF', stroke: '#D79B00' },
  infra:  { fill: '#F8CECC', stroke: '#B85450' },
};

class Page {
  constructor(name) { this.name = name; this.cells = []; this.n = 0; this.geo = {}; }
  id() { return `${this.name.replace(/\W/g, '').slice(0, 6)}-${++this.n}`; }
  raw(xml) { this.cells.push(xml); }

  // ---------- Diagrama de clases ----------
  cls(key, x, y, c) {
    const pal = PAL[c.pal || 'dom'];
    const head = [];
    if (c.st) head.push(`<i>&lt;&lt;${h(c.st)}&gt;&gt;</i>`);
    head.push(c.abstract ? `<b><i>${h(c.name)}</i></b>` : `<b>${h(c.name)}</b>`);
    const attrs = c.a || [], ops = c.m || [];
    let html = `<p style="margin:0;margin-top:4px;text-align:center;">${head.join('<br/>')}</p>`;
    if (attrs.length || !c.noAttrSection) html += `<hr size="1" style="border-style:solid;"/><p style="margin:0;margin-left:6px;">${attrs.map(h).join('<br/>')}</p>`;
    if (ops.length) html += `<hr size="1" style="border-style:solid;"/><p style="margin:0;margin-left:6px;">${ops.map(h).join('<br/>')}</p>`;
    const longest = Math.max(c.name.length + 4, (c.st || '').length + 6, ...attrs.map(s => s.length), ...ops.map(s => s.length));
    const w = c.w || Math.max(150, Math.ceil(longest * 6.1 + 22));
    const hgt = 10 + head.length * 14 + (attrs.length || !c.noAttrSection ? 10 + Math.max(attrs.length, 1) * 14 : 0) + (ops.length ? 10 + ops.length * 14 : 0) + 6;
    const id = this.id();
    this.geo[key] = { id, x, y, w, h: hgt };
    this.raw(`<mxCell id="${id}" value="${xmlEsc(html)}" style="verticalAlign=top;align=left;overflow=fill;html=1;whiteSpace=wrap;fontSize=11;fontFamily=Helvetica;fillColor=${pal.fill};strokeColor=${pal.stroke};${c.dashed ? 'dashed=1;' : ''}" vertex="1" parent="1"><mxGeometry x="${x}" y="${y}" width="${w}" height="${hgt}" as="geometry"/></mxCell>`);
    return this.geo[key];
  }

  // ---------- Tabla ER ----------
  table(key, x, y, t) {
    const color = t.color;
    const rows = t.cols.map(([k, n, ty]) => {
      const kk = k ? `<b>${h(k)}</b>` : '';
      const nn = /PK/.test(k) ? `<u>${h(n)}</u>` : h(n);
      return `<tr><td style="padding:0 4px;width:34px;color:#555;font-size:10px;">${kk}</td><td style="padding:0 4px;">${nn}</td><td style="padding:0 4px;color:#777;text-align:right;">${h(ty)}</td></tr>`;
    }).join('');
    const cons = (t.cons || []).map(s => `<tr><td colspan="3" style="padding:0 4px;font-size:10px;color:#444;"><i>${h(s)}</i></td></tr>`).join('');
    const html = `<table style="width:100%;border-collapse:collapse;font-size:11px;">`
      + `<tr><td colspan="3" style="background:${color};color:#fff;font-weight:bold;padding:3px 4px;text-align:center;">${h(t.name)}</td></tr>`
      + rows + (cons ? `<tr><td colspan="3" style="border-top:1px solid #ccc;padding:0;"></td></tr>` + cons : '') + `</table>`;
    const longest = Math.max(...t.cols.map(([k, n, ty]) => n.length + ty.length + 8), ...(t.cons || []).map(s => s.length * 0.92), t.name.length + 4);
    const w = t.w || Math.max(230, Math.ceil(longest * 6.2 + 30));
    const hgt = 22 + t.cols.length * 13 + (t.cons ? 4 + t.cons.length * 13 : 0) + 8;
    const id = this.id();
    this.geo[key] = { id, x, y, w, h: hgt };
    this.raw(`<mxCell id="${id}" value="${xmlEsc(html)}" style="html=1;overflow=fill;whiteSpace=wrap;verticalAlign=top;align=left;fillColor=#FFFFFF;strokeColor=${color};strokeWidth=1.5;spacing=0;fontSize=11;" vertex="1" parent="1"><mxGeometry x="${x}" y="${y}" width="${w}" height="${hgt}" as="geometry"/></mxCell>`);
    return this.geo[key];
  }

  note(x, y, w, hgt, html, opts = {}) {
    const id = this.id();
    this.raw(`<mxCell id="${id}" value="${xmlEsc(html)}" style="${opts.shape || 'shape=note;size=14;'}whiteSpace=wrap;html=1;align=left;verticalAlign=top;spacing=8;fontSize=${opts.fs || 11};fillColor=${opts.fill || '#FFFDE7'};strokeColor=${opts.stroke || '#BDB76B'};" vertex="1" parent="1"><mxGeometry x="${x}" y="${y}" width="${w}" height="${hgt}" as="geometry"/></mxCell>`);
    return id;
  }
  text(x, y, w, hgt, html, fs = 20) {
    const id = this.id();
    this.raw(`<mxCell id="${id}" value="${xmlEsc(html)}" style="text;html=1;align=left;verticalAlign=top;fontSize=${fs};whiteSpace=wrap;" vertex="1" parent="1"><mxGeometry x="${x}" y="${y}" width="${w}" height="${hgt}" as="geometry"/></mxCell>`);
  }
  frame(x, y, w, hgt, label, color) {
    const id = this.id();
    this.cells.unshift(`<mxCell id="${id}" value="${xmlEsc(label)}" style="rounded=1;arcSize=2;html=1;whiteSpace=wrap;verticalAlign=top;align=left;spacingLeft=10;spacingTop=4;fontStyle=1;fontSize=13;dashed=1;fillColor=${color};strokeColor=#999999;opacity=60;" vertex="1" parent="1"><mxGeometry x="${x}" y="${y}" width="${w}" height="${hgt}" as="geometry"/></mxCell>`);
  }

  edge(a, b, style, o = {}) {
    const A = this.geo[a], B = this.geo[b];
    if (!A || !B) throw new Error(`edge ${a}->${b}`);
    const id = this.id();
    const base = 'html=1;rounded=0;fontSize=10;edgeStyle=orthogonalEdgeStyle;jumpStyle=arc;jumpSize=8;';
    const ports = (o.ex !== undefined ? `exitX=${o.ex};exitY=${o.ey};exitDx=0;exitDy=0;` : '') + (o.nx !== undefined ? `entryX=${o.nx};entryY=${o.ny};entryDx=0;entryDy=0;` : '');
    const pts = o.pts ? `<Array as="points">${o.pts.map(([px, py]) => `<mxPoint x="${px}" y="${py}"/>`).join('')}</Array>` : '';
    this.raw(`<mxCell id="${id}" value="${xmlEsc(o.label || '')}" style="${base}${ports}${style}" edge="1" parent="1" source="${A.id}" target="${B.id}"><mxGeometry relative="1" as="geometry">${pts}</mxGeometry></mxCell>`);
    const lab = (txt, pos) => {
      if (!txt) return;
      this.raw(`<mxCell id="${this.id()}" value="${xmlEsc(txt)}" style="edgeLabel;resizable=0;html=1;fontSize=10;labelBackgroundColor=#FFFFFF;" vertex="1" connectable="0" parent="${id}"><mxGeometry x="${pos}" relative="1" as="geometry"><mxPoint x="0" y="-9" as="offset"/></mxGeometry></mxCell>`);
    };
    lab(o.s, -0.82); lab(o.t, 0.82);
  }

  xml(dx, dy) {
    return `<diagram id="${this.name.replace(/\W/g, '')}" name="${xmlEsc(this.name)}"><mxGraphModel dx="${dx}" dy="${dy}" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="0" pageScale="1" math="0" shadow="0"><root><mxCell id="0"/><mxCell id="1" parent="0"/>${this.cells.join('')}</root></mxGraphModel></diagram>`;
  }
}

const S = {
  assoc: 'endArrow=open;endFill=0;endSize=10;',
  line: 'endArrow=none;',
  comp: 'startArrow=diamondThin;startFill=1;startSize=16;endArrow=none;',
  aggr: 'startArrow=diamondThin;startFill=0;startSize=16;endArrow=none;',
  inh: 'endArrow=block;endFill=0;endSize=14;',
  real: 'endArrow=block;endFill=0;endSize=14;dashed=1;',
  dep: 'endArrow=open;endFill=0;endSize=10;dashed=1;',
};

// Coloca columnas: cada columna es una lista de [key, def]; devuelve posiciones con alto calculado.
function columns(p, x0, y0, cols, gapX, gapY, kind = 'cls') {
  let x = x0;
  for (const col of cols) {
    let y = y0, maxW = 0;
    const placed = [];
    for (const [key, def] of col) {
      const g = kind === 'cls' ? p.cls(key, x, y, def) : p.table(key, x, y, def);
      placed.push(g);
      maxW = Math.max(maxW, g.w);
      y += g.h + gapY;
    }
    x += maxW + gapX;
  }
  return x;
}

function legend(p, x, y, items) {
  p.raw(`<mxCell id="${p.id()}" value="&lt;b&gt;Convenciones&lt;/b&gt;" style="text;html=1;fontSize=12;align=left;" vertex="1" parent="1"><mxGeometry x="${x}" y="${y}" width="200" height="20" as="geometry"/></mxCell>`);
  let yy = y + 24;
  for (const [pal, txt] of items) {
    p.raw(`<mxCell id="${p.id()}" value="" style="rounded=0;fillColor=${PAL[pal]?.fill || pal};strokeColor=${PAL[pal]?.stroke || '#666'};" vertex="1" parent="1"><mxGeometry x="${x}" y="${yy}" width="26" height="16" as="geometry"/></mxCell>`);
    p.raw(`<mxCell id="${p.id()}" value="${xmlEsc(txt)}" style="text;html=1;fontSize=11;align=left;verticalAlign=middle;" vertex="1" parent="1"><mxGeometry x="${x + 34}" y="${yy - 2}" width="330" height="20" as="geometry"/></mxCell>`);
    yy += 22;
  }
  return yy;
}

// =====================================================================
// Utilidades de layout
// =====================================================================
const SUB = 'Ingeniería del Software 2 (ISS0666) · Universidad Católica de Oriente · v1.0 · octubre 2026';
const measure = def => new Page('tmp').cls('k', 0, 0, def);
const bottomOf = (p, keys) => Math.max(...keys.map(k => p.geo[k].y + p.geo[k].h));
const rightOf = (p, keys) => Math.max(...keys.map(k => p.geo[k].x + p.geo[k].w));
const fy = (p, k, absY) => +((absY - p.geo[k].y) / p.geo[k].h).toFixed(4);
const fx = (p, k, absX) => +((absX - p.geo[k].x) / p.geo[k].w).toFixed(4);
const cy = (p, k, f = 0.5) => p.geo[k].y + p.geo[k].h * f;
const cx = (p, k, f = 0.5) => p.geo[k].x + p.geo[k].w * f;
const enumBox = (name, vals) => ({ name, st: 'enumeration', pal: 'enum', a: vals, m: [] });
const header = (p, title, sub) => {
  p.text(40, 10, 2200, 34, `<b>${title}</b>`, 22);
  p.text(40, 42, 2200, 22, `<span style="color:#555">${sub}</span>`, 12);
};

// =====================================================================
// PÁGINA 1 — DIAGRAMA DE CLASES DEL DOMINIO (FlowRide.Domain)
// =====================================================================
const d = new Page('1. Clases - Dominio');
header(d, 'FlowRide — Diagrama de clases del dominio', `FlowRide.Domain · modelo de una instancia de operador (Silo) · ${SUB}`);

const D = {
  Rol: { name: 'Rol', a: ['+ Id: Guid', '+ Nombre: string', '+ Descripcion: string'], m: [] },
  Usuario: { name: 'Usuario', a: ['+ Id: Guid', '+ Nombre: string', '+ Correo: string?', '+ TelefonoWhatsApp: string', '+ IdentidadExternaId: string?', '+ Activo: bool', '+ CreadoEn: DateTimeOffset', '+ Version: uint'], m: ['+ Activar(): void', '+ Desactivar(): void', '+ CambiarRol(rol: Rol): void'] },
  Pasajero: { name: 'Pasajero', a: ['+ CodigoInterno: string'], m: ['+ SolicitarViaje(origen: string, destino: string, servicio: FranjaHoraria,', '      pasajeros: int, urgente: bool, esPersonal: bool): SolicitudViaje', '+ Calificar(viaje: Viaje, puntaje: int, comentario: string?): Calificacion'] },
  EmpresaCliente: { name: 'EmpresaCliente', a: ['+ Id: Guid', '+ Nombre: string', '+ Nit: string', '+ CorreoContacto: string', '+ Activa: bool'], m: [] },
  Itinerario: { name: 'Itinerario', a: ['+ Id: Guid', '+ CodigoServicio: string', '+ Fecha: DateOnly', '+ Origen: string', '+ Destino: string', '+ HoraSalida: TimeOnly', '+ HoraLlegada: TimeOnly'], m: [] },

  SolicitudViaje: { name: 'SolicitudViaje', pal: 'rf10', a: ['+ Id: Guid', '+ DireccionOrigen: string', '+ DireccionDestino: string', '+ FechaHoraServicio: DateTimeOffset', '+ DuracionEstimada: TimeSpan', '+ CantidadPasajeros: int', '+ Urgente: bool', '+ EsPersonal: bool', '+ Canal: CanalSolicitud', '+ Estado: EstadoSolicitud', '+ RegistradaEn: DateTimeOffset', '+ Version: uint'], m: ['+ Franja(): FranjaHoraria', '+ MarcarAsignada(a: Asignacion, actor: Usuario?): void', '+ MarcarSinCandidatos(motivo: string): void', '+ Cancelar(motivo: string, actor: Usuario): CancelacionSolicitud', '- CambiarEstado(nuevo: EstadoSolicitud, actor: Usuario?, motivo: string?): void'] },
  EstadoSolicitudHistorial: { name: 'EstadoSolicitudHistorial', a: ['+ Id: Guid', '+ EstadoAnterior: EstadoSolicitud?', '+ EstadoNuevo: EstadoSolicitud', '+ FechaHora: DateTimeOffset', '+ Actor: Usuario?', '+ Motivo: string?'], m: [] },
  CancelacionSolicitud: { name: 'CancelacionSolicitud', a: ['+ Id: Guid', '+ Motivo: string', '+ FechaHora: DateTimeOffset', '+ CanceladaPor: Usuario'], m: [] },
  FranjaHoraria: { name: 'FranjaHoraria', st: 'value object', pal: 'vo', a: ['+ Inicio: DateTimeOffset', '+ Fin: DateTimeOffset'], m: ['+ SeSolapaCon(otra: FranjaHoraria): bool', '+ EstaContenidaEn(otra: FranjaHoraria): bool'] },

  Asignacion: { name: 'Asignacion', pal: 'rf10', a: ['+ Id: Guid', '+ Origen: OrigenAsignacion', '+ Estado: EstadoAsignacion', '+ Franja: FranjaHoraria', '+ AsignadaEn: DateTimeOffset', '+ DecisionId: Guid?', '+ Version: uint'], m: ['+ EstaVigente(): bool', '+ Aceptar(): void', '+ Rechazar(motivo: string): void', '+ Iniciar(): Viaje', '+ Cancelar(motivo: string): void', '+ Reemplazar(c: Conductor, v: Vehiculo, actor: Usuario): Asignacion'] },

  Conductor: { name: 'Conductor', pal: 'rf10', a: ['+ NumeroLicencia: string', '+ CategoriaLicencia: string', '+ VencimientoLicencia: DateOnly', '+ Estado: EstadoConductor', '+ FechaUltimaAsignacion: DateTimeOffset?'], m: ['+ LicenciaVigenteEn(fecha: DateOnly): bool', '+ TieneTurnoQueCubre(franja: FranjaHoraria): bool', '+ AceptarAsignacion(a: Asignacion): void', '+ RechazarAsignacion(a: Asignacion, motivo: string): void', '+ AbandonarTurno(turno: Turno, motivo: string): void'] },
  Turno: { name: 'Turno', pal: 'rf10', a: ['+ Id: Guid', '+ Inicio: DateTimeOffset', '+ Fin: DateTimeOffset', '+ Estado: EstadoTurno', '+ Version: uint'], m: ['+ Cubre(franja: FranjaHoraria): bool', '+ Iniciar(): void', '+ Finalizar(): void', '+ MarcarAbandonado(motivo: string): void'] },
  DocumentoConductor: { name: 'DocumentoConductor', a: ['+ Id: Guid', '+ Tipo: TipoDocumento', '+ ReferenciaBlob: string', '+ FechaVencimiento: DateOnly?'], m: [] },
  Vehiculo: { name: 'Vehiculo', pal: 'rf10', a: ['+ Id: Guid', '+ Placa: string', '+ Marca: string', '+ Modelo: string', '+ Capacidad: int', '+ Activo: bool', '+ VencimientoRtm: DateOnly', '+ VencimientoSoat: DateOnly', '+ Version: uint'], m: ['+ DocumentacionVigenteEn(fecha: DateOnly): bool', '+ AdmiteCapacidad(pasajeros: int): bool'] },

  Viaje: { name: 'Viaje', a: ['+ Id: Guid', '+ Estado: EstadoViaje', '+ EtaProgramada: DateTimeOffset', '+ FechaHoraInicio: DateTimeOffset?', '+ FechaHoraFin: DateTimeOffset?', '+ Version: uint'], m: ['+ Iniciar(): void', '+ Finalizar(): void', '+ RegistrarParada(direccion: string, orden: int): Parada', '+ RegistrarIncidente(tipo: TipoIncidente, desc: string, por: Usuario): Incidente'] },
  Parada: { name: 'Parada', a: ['+ Id: Guid', '+ Orden: int', '+ Direccion: string', '+ HoraEstimada: DateTimeOffset', '+ HoraReal: DateTimeOffset?'], m: [] },
  Incidente: { name: 'Incidente', a: ['+ Id: Guid', '+ Tipo: TipoIncidente', '+ Descripcion: string', '+ FechaHora: DateTimeOffset', '+ ReportadoPor: Usuario'], m: [] },
  Calificacion: { name: 'Calificacion', pal: 'v1', a: ['+ Id: Guid', '+ Puntaje: int {1..5}', '+ Comentario: string?', '+ FechaHora: DateTimeOffset'], m: [] },
  RegistroUbicacion: { name: 'RegistroUbicacion', pal: 'v1', a: ['+ Id: long', '+ TimestampOrigen: DateTimeOffset', '+ TimestampRecepcion: DateTimeOffset', '+ Latitud: decimal', '+ Longitud: decimal', '+ Calidad: string?', '+ Fuente: string'], m: [] },
  DesviacionEta: { name: 'DesviacionEta', pal: 'v1', a: ['+ Id: Guid', '+ DetectadaEn: DateTimeOffset', '+ EtaEstimada: DateTimeOffset', '+ MinutosDesviacion: int', '+ Estado: EstadoAlerta'], m: [] },

  EstadoSolicitud: enumBox('EstadoSolicitud', ['Pendiente', 'Asignada', 'SinCandidatos', 'EnCurso', 'Completada', 'Cancelada']),
  CanalSolicitud: enumBox('CanalSolicitud', ['WhatsApp', 'PanelWeb']),
  EstadoAsignacion: enumBox('EstadoAsignacion', ['Asignada', 'Aceptada', 'Rechazada', 'EnCurso', 'Finalizada', 'Cancelada', 'Reemplazada']),
  OrigenAsignacion: enumBox('OrigenAsignacion', ['Automatica', 'Manual']),
  EstadoConductor: enumBox('EstadoConductor', ['Activo', 'Suspendido', 'Inactivo']),
  EstadoTurno: enumBox('EstadoTurno', ['Programado', 'Activo', 'Finalizado', 'Abandonado']),
  TipoDocumento: enumBox('TipoDocumento', ['Licencia', 'Rtm', 'Soat', 'Otro']),
  EstadoViaje: enumBox('EstadoViaje', ['Programado', 'EnCurso', 'Finalizado', 'Cancelado']),
  TipoIncidente: enumBox('TipoIncidente', ['Retraso', 'FallaMecanica', 'AbandonoTurno', 'Accidente', 'Otro']),
  EstadoAlerta: enumBox('EstadoAlerta', ['Abierta', 'Atendida', 'Descartada']),
};
const Y0 = 130;
const P1 = (k, x, y) => d.cls(k, x, y, D[k]);
const g1 = d.geo;

// Columna 1 — personas y clientes
P1('Rol', 40, Y0);
P1('Usuario', 40, g1.Rol.y + g1.Rol.h + 60);
P1('Pasajero', 40, g1.Usuario.y + g1.Usuario.h + 70);
P1('EmpresaCliente', 40, g1.Pasajero.y + g1.Pasajero.h + 60);
P1('Itinerario', 40, g1.EmpresaCliente.y + g1.EmpresaCliente.h + 60);
const x2 = rightOf(d, ['Rol', 'Usuario', 'Pasajero', 'EmpresaCliente', 'Itinerario']) + 200;

// Columna 2 — solicitud
P1('SolicitudViaje', x2, Y0);
P1('EstadoSolicitud', x2, g1.SolicitudViaje.y + g1.SolicitudViaje.h + 60);
P1('CanalSolicitud', g1.EstadoSolicitud.x + g1.EstadoSolicitud.w + 30, g1.EstadoSolicitud.y);
P1('EstadoSolicitudHistorial', x2, g1.EstadoSolicitud.y + g1.EstadoSolicitud.h + 60);
P1('CancelacionSolicitud', x2, g1.EstadoSolicitudHistorial.y + g1.EstadoSolicitudHistorial.h + 50);
P1('FranjaHoraria', Math.max(rightOf(d, ['EstadoSolicitudHistorial', 'CanalSolicitud']) + 60, x2 + 260), g1.EstadoSolicitudHistorial.y);
const x3 = rightOf(d, ['SolicitudViaje', 'FranjaHoraria']) + 180;

// Columna 3 — asignación
P1('Asignacion', x3, Y0);
P1('EstadoAsignacion', x3, g1.Asignacion.y + g1.Asignacion.h + 60);
P1('OrigenAsignacion', g1.EstadoAsignacion.x + g1.EstadoAsignacion.w + 30, g1.EstadoAsignacion.y);
const viajeW = measure(D.Viaje).w;
const x4 = x3 + Math.max(g1.Asignacion.w, viajeW, g1.OrigenAsignacion.x + g1.OrigenAsignacion.w - x3) + 240;

// Columna 4 — recursos
P1('Conductor', x4, Y0);
P1('EstadoConductor', g1.Conductor.x + g1.Conductor.w + 70, Y0 + 10);
P1('Turno', x4, g1.Conductor.y + g1.Conductor.h + 80);
P1('DocumentoConductor', g1.Turno.x + g1.Turno.w + 60, g1.Turno.y);
P1('EstadoTurno', x4, g1.Turno.y + g1.Turno.h + 50);
P1('TipoDocumento', g1.DocumentoConductor.x, g1.DocumentoConductor.y + g1.DocumentoConductor.h + 50);
P1('Vehiculo', x4, bottomOf(d, ['EstadoTurno', 'TipoDocumento']) + 70);

// Bloque inferior — ejecución del viaje (centrado bajo la columna 3)
const sz = k => measure(D[k]);
const hV = sz('Viaje').h, hReg = sz('RegistroUbicacion').h, hCal = sz('Calificacion').h, hPar = sz('Parada').h;
let yV = Math.max(
  bottomOf(d, ['Itinerario', 'CancelacionSolicitud', 'FranjaHoraria', 'EstadoAsignacion', 'OrigenAsignacion']) + 110,
  bottomOf(d, ['Vehiculo']) + 80 + hReg + 50 + hCal / 2 - hV / 2,
  bottomOf(d, ['Itinerario', 'CancelacionSolicitud']) + 80 + hPar + 40 - hV / 2 + 60,
);
P1('Viaje', x3, yV);
P1('EstadoViaje', x3, g1.Viaje.y + g1.Viaje.h + 60);
// pila derecha: RegistroUbicacion / Calificacion / DesviacionEta
const xR = g1.Viaje.x + g1.Viaje.w + 130;
P1('Calificacion', xR, cy(d, 'Viaje') - hCal / 2);
P1('RegistroUbicacion', xR, g1.Calificacion.y - 50 - hReg);
P1('DesviacionEta', xR, g1.Calificacion.y + g1.Calificacion.h + 50);
P1('EstadoAlerta', g1.DesviacionEta.x + g1.DesviacionEta.w + 70, g1.DesviacionEta.y + 5);
// pila izquierda: Parada / Incidente (+ TipoIncidente)
const wL = Math.max(sz('Parada').w, sz('Incidente').w);
const xL = g1.Viaje.x - 130 - wL;
P1('Parada', xL, cy(d, 'Viaje') - 20 - hPar);
P1('Incidente', xL, cy(d, 'Viaje') + 20);
P1('TipoIncidente', g1.Incidente.x - 70 - sz('TipoIncidente').w, g1.Incidente.y);

// ---------- Relaciones ----------
// Generalización
d.edge('Pasajero', 'Usuario', S.inh, { ex: fx(d, 'Pasajero', cx(d, 'Usuario')), ey: 0, nx: 0.5, ny: 1 });
d.edge('Conductor', 'Usuario', S.inh, { ex: 0.5, ey: 0, nx: 1, ny: 0.5, pts: [[cx(d, 'Conductor'), Y0 - 45], [x2 - 120, Y0 - 45], [x2 - 120, cy(d, 'Usuario')]] });
// Columna 1
d.edge('Usuario', 'Rol', S.assoc, { s: '0..*', t: '1', ex: fx(d, 'Usuario', cx(d, 'Rol')), ey: 0, nx: 0.5, ny: 1 });
d.edge('Pasajero', 'EmpresaCliente', S.assoc, { s: '0..*', t: '1', ex: fx(d, 'Pasajero', cx(d, 'EmpresaCliente')), ey: 1, nx: 0.5, ny: 0 });
d.edge('EmpresaCliente', 'Itinerario', S.line, { s: '1', t: '0..*', ex: fx(d, 'EmpresaCliente', cx(d, 'Itinerario')), ey: 1, nx: 0.5, ny: 0 });
// Solicitud → columna 1 (canales escalonados para evitar cruces)
d.edge('SolicitudViaje', 'Pasajero', S.assoc, { s: '0..*', t: '1', ex: 0, ey: 0.3, nx: 1, ny: 0.5, pts: [[x2 - 95, cy(d, 'SolicitudViaje', 0.3)], [x2 - 95, cy(d, 'Pasajero')]] });
d.edge('SolicitudViaje', 'EmpresaCliente', S.assoc, { s: '0..*', t: '0..1', ex: 0, ey: 0.5, nx: 1, ny: 0.5, pts: [[x2 - 75, cy(d, 'SolicitudViaje', 0.5)], [x2 - 75, cy(d, 'EmpresaCliente')]] });
d.edge('SolicitudViaje', 'Itinerario', S.assoc, { s: '0..*', t: '0..1', ex: 0, ey: 0.7, nx: 1, ny: 0.5, pts: [[x2 - 55, cy(d, 'SolicitudViaje', 0.7)], [x2 - 55, cy(d, 'Itinerario')]] });
d.edge('SolicitudViaje', 'CancelacionSolicitud', S.comp, { s: '1', t: '0..1', ex: 0, ey: 0.85, nx: 0, ny: 0.5, pts: [[x2 - 35, cy(d, 'SolicitudViaje', 0.85)], [x2 - 35, cy(d, 'CancelacionSolicitud')]] });
d.edge('SolicitudViaje', 'EstadoSolicitudHistorial', S.comp, { s: '1', t: '1..*', ex: 0, ey: 0.95, nx: 0, ny: 0.5, pts: [[x2 - 18, cy(d, 'SolicitudViaje', 0.95)], [x2 - 18, cy(d, 'EstadoSolicitudHistorial')]] });
// Enumeraciones y value object de la solicitud
d.edge('SolicitudViaje', 'EstadoSolicitud', S.dep, { ex: fx(d, 'SolicitudViaje', cx(d, 'EstadoSolicitud')), ey: 1, nx: 0.5, ny: 0 });
d.edge('SolicitudViaje', 'CanalSolicitud', S.dep, { ex: fx(d, 'SolicitudViaje', cx(d, 'CanalSolicitud')), ey: 1, nx: 0.5, ny: 0 });
d.edge('EstadoSolicitudHistorial', 'EstadoSolicitud', S.dep, { ex: fx(d, 'EstadoSolicitudHistorial', cx(d, 'EstadoSolicitud')), ey: 0, nx: 0.5, ny: 1 });
{
  const xf = Math.min(g1.FranjaHoraria.x + 40, g1.SolicitudViaje.x + g1.SolicitudViaje.w - 20);
  d.edge('SolicitudViaje', 'FranjaHoraria', S.dep, { label: '«calcula»', ex: fx(d, 'SolicitudViaje', xf), ey: 1, nx: fx(d, 'FranjaHoraria', xf), ny: 0 });
}
// Solicitud ↔ Asignación (misma altura absoluta para línea recta)
d.edge('SolicitudViaje', 'Asignacion', S.line, { label: '{máx. 1 vigente}', s: '1', t: '0..*', ex: 1, ey: fy(d, 'SolicitudViaje', Y0 + 40), nx: 0, ny: fy(d, 'Asignacion', Y0 + 40) });
d.edge('Asignacion', 'FranjaHoraria', S.dep, { ex: 0, ey: 0.85, nx: 1, ny: 0.5, pts: [[x3 - 40, cy(d, 'Asignacion', 0.85)], [x3 - 40, cy(d, 'FranjaHoraria')]] });
d.edge('Asignacion', 'EstadoAsignacion', S.dep, { ex: fx(d, 'Asignacion', cx(d, 'EstadoAsignacion')), ey: 1, nx: 0.5, ny: 0 });
d.edge('Asignacion', 'OrigenAsignacion', S.dep, { ex: fx(d, 'Asignacion', cx(d, 'OrigenAsignacion')), ey: 1, nx: 0.5, ny: 0 });
// Autoasociación de reemplazo (RF-12)
d.edge('Asignacion', 'Asignacion', S.line, { label: 'reemplaza', s: '0..1', t: '0..1', ex: 0.55, ey: 0, nx: 0.8, ny: 0, pts: [[cx(d, 'Asignacion', 0.55), Y0 - 22], [cx(d, 'Asignacion', 0.8), Y0 - 22]] });
// Asignación → recursos
d.edge('Asignacion', 'Conductor', S.assoc, { s: '0..*', t: '1', ex: 1, ey: fy(d, 'Asignacion', Y0 + 60), nx: 0, ny: fy(d, 'Conductor', Y0 + 60) });
d.edge('Asignacion', 'Vehiculo', S.assoc, { s: '0..*', t: '1', ex: 1, ey: 0.55, nx: 0, ny: 0.6, pts: [[x4 - 45, cy(d, 'Asignacion', 0.55)], [x4 - 45, cy(d, 'Vehiculo', 0.6)]] });
d.edge('Vehiculo', 'Conductor', S.line, { label: 'habitual', s: '0..1', t: '0..1', ex: 0, ey: 0.3, nx: 0, ny: 0.85, pts: [[x4 - 22, cy(d, 'Vehiculo', 0.3)], [x4 - 22, cy(d, 'Conductor', 0.85)]] });
// Conductor y sus partes
d.edge('Conductor', 'Turno', S.comp, { s: '1', t: '0..*', ex: fx(d, 'Conductor', cx(d, 'Turno')), ey: 1, nx: 0.5, ny: 0 });
d.edge('Conductor', 'DocumentoConductor', S.comp, { s: '1', t: '0..*', ex: 1, ey: 0.85, nx: 0.5, ny: 0, pts: [[cx(d, 'DocumentoConductor'), cy(d, 'Conductor', 0.85)]] });
d.edge('Conductor', 'EstadoConductor', S.dep, { ex: 1, ey: fy(d, 'Conductor', cy(d, 'EstadoConductor')), nx: 0, ny: 0.5 });
d.edge('Turno', 'EstadoTurno', S.dep, { ex: fx(d, 'Turno', cx(d, 'EstadoTurno')), ey: 1, nx: 0.5, ny: 0 });
d.edge('DocumentoConductor', 'TipoDocumento', S.dep, { ex: fx(d, 'DocumentoConductor', cx(d, 'TipoDocumento')), ey: 1, nx: 0.5, ny: 0 });
// Asignación → Viaje (vertical recta por la derecha de las enumeraciones)
{
  const xa = Math.max(g1.OrigenAsignacion.x + g1.OrigenAsignacion.w + 40, g1.Asignacion.x + g1.Asignacion.w * 0.85);
  d.edge('Asignacion', 'Viaje', S.line, { s: '1', t: '0..1', ex: fx(d, 'Asignacion', xa), ey: 1, nx: fx(d, 'Viaje', xa), ny: 0 });
}
d.edge('Viaje', 'EstadoViaje', S.dep, { ex: fx(d, 'Viaje', cx(d, 'EstadoViaje')), ey: 1, nx: 0.5, ny: 0 });
// Viaje → pila izquierda
d.edge('Viaje', 'Parada', S.comp, { s: '1', t: '0..*', ex: 0, ey: 0.2, nx: 1, ny: 0.5, pts: [[g1.Viaje.x - 60, cy(d, 'Viaje', 0.2)], [g1.Viaje.x - 60, cy(d, 'Parada')]] });
d.edge('Viaje', 'Incidente', S.comp, { s: '1', t: '0..*', ex: 0, ey: 0.8, nx: 1, ny: 0.5, pts: [[g1.Viaje.x - 60, cy(d, 'Viaje', 0.8)], [g1.Viaje.x - 60, cy(d, 'Incidente')]] });
d.edge('Incidente', 'TipoIncidente', S.dep, { ex: 0, ey: fy(d, 'Incidente', cy(d, 'TipoIncidente')), nx: 1, ny: 0.5 });
// Viaje → pila derecha
d.edge('Viaje', 'RegistroUbicacion', S.line, { s: '0..1', t: '0..*', ex: 1, ey: 0.2, nx: 0, ny: 0.5, pts: [[g1.Viaje.x + g1.Viaje.w + 60, cy(d, 'Viaje', 0.2)], [g1.Viaje.x + g1.Viaje.w + 60, cy(d, 'RegistroUbicacion')]] });
d.edge('Viaje', 'Calificacion', S.line, { s: '1', t: '0..*', ex: 1, ey: fy(d, 'Viaje', cy(d, 'Calificacion')), nx: 0, ny: 0.5 });
d.edge('Viaje', 'DesviacionEta', S.comp, { s: '1', t: '0..*', ex: 1, ey: 0.8, nx: 0, ny: 0.5, pts: [[g1.Viaje.x + g1.Viaje.w + 60, cy(d, 'Viaje', 0.8)], [g1.Viaje.x + g1.Viaje.w + 60, cy(d, 'DesviacionEta')]] });
d.edge('DesviacionEta', 'EstadoAlerta', S.dep, { ex: 1, ey: fy(d, 'DesviacionEta', cy(d, 'EstadoAlerta')), nx: 0, ny: 0.5 });
{
  const xv = Math.max(g1.RegistroUbicacion.x, g1.Vehiculo.x) + 50;
  d.edge('RegistroUbicacion', 'Vehiculo', S.assoc, { s: '0..*', t: '1', ex: fx(d, 'RegistroUbicacion', xv), ey: 0, nx: fx(d, 'Vehiculo', xv), ny: 1 });
}

{
  const yB = Math.max(...Object.values(d.geo).map(g => g.y + g.h)) + 70;
  legend(d, 40, yB, [
    ['rf10', 'Núcleo RF-10 (asignación automática)'],
    ['dom', 'Dominio operativo'],
    ['v1', 'Capacidades V1: calificación y telemetría (RF-54/55/56)'],
    ['vo', 'Value object'],
    ['enum', 'Enumeración'],
  ]);
  d.note(470, yB, 1180, 200,
    '<b>Notas de diseño</b><br/>' +
    '• Las relaciones entre clases se expresan como asociaciones; las llaves foráneas solo aparecen en el modelo entidad-relación (página 3).<br/>' +
    '• Las referencias al actor de una acción (Actor, CanceladaPor, ReportadoPor: Usuario) se muestran en notación de atributo para no saturar el diagrama.<br/>' +
    '• Asignacion.DecisionId referencia por identidad al decision log del motor (FlowRide.Application, página 2): los módulos no comparten objetos.<br/>' +
    '• La <b>disponibilidad</b> del conductor no se persiste como estado: se deriva de sus turnos y asignaciones vigentes, lo que hace reproducible la decisión.<br/>' +
    '• <b>Version</b> es el token de concurrencia optimista (ADR-0007). Una solicitud tiene a lo sumo una asignación vigente.<br/>' +
    '• EmpresaCliente es el cliente empresarial atendido (aerolínea, minera…), no el tenant: el aislamiento entre operadores lo da el modelo Silo.<br/>' +
    '• SolicitudViaje reemplaza a SolicitudTransporte. Evolución declarada del modelo (contexto v10, Capa 4): Calificacion, CancelacionSolicitud, EstadoSolicitudHistorial, EsPersonal, RegistroUbicacion.<br/>' +
    '• La telemetría (RegistroUbicacion, DesviacionEta) sirve para monitoreo y ETA; <b>no</b> participa en la asignación (ADR-0005, ADR-0009).');
  d.note(1680, yB, 520, 200,
    '<b>Notación</b><br/>' +
    '◆ composición: la parte no existe sin el todo.<br/>' +
    '△ generalización (herencia).<br/>' +
    '→ asociación navegable, con multiplicidad en cada extremo.<br/>' +
    '⇢ dependencia (línea punteada): uso de un tipo enumerado o value object como atributo, parámetro o retorno.<br/>' +
    '{máx. 1 vigente}: restricción de la asociación (ver índice único parcial en la página 3).', { fill: '#F7F9FC', stroke: '#9AA9C0' });
}

// =====================================================================
// PÁGINA 2 — DIAGRAMA DE CLASES DEL MOTOR DE ASIGNACIÓN (ADR-0009)
// =====================================================================
const m = new Page('2. Clases - Motor de asignación');
header(m, 'FlowRide — Diagrama de clases del motor de asignación (RF-10 · ADR-0009)', `Strategy/Policy con composición determinista · FlowRide.Application/Assignment · ${SUB}`);

const M = {
  Handler: { name: 'AssignPendingRequestHandler', st: 'caso de uso', pal: 'rf10', a: ['- engine: AssignmentEngine', '- solicitudes: ISolicitudViajeRepository', '- candidates: ICandidateProvider', '- configs: IAssignmentConfigurationProvider', '- decisions: IDecisionLogRepository', '- uow: IUnitOfWork', '- clock: IClock'], m: ['+ HandleAsync(solicitudId: Guid, ct: CancellationToken): Task<AssignmentResult>', '+ HandleAllPendingAsync(ct: CancellationToken): Task<int>'] },
  Engine: { name: 'AssignmentEngine', pal: 'rf10', a: ['- catalog: IReadOnlyDictionary<string, IAssignmentRule>', '- validator: ConfigurationValidator'], m: ['+ Decide(ctx: AssignmentContext): AssignmentResult', '- ApplyElimination(ctx: AssignmentContext): IReadOnlyList<CandidateEvaluation>', '- ApplyPriority(ctx: AssignmentContext, evals: IReadOnlyList<CandidateEvaluation>): void', '- ApplyScoring(ctx: AssignmentContext, evals: IReadOnlyList<CandidateEvaluation>): void', '- Rank(evals: IReadOnlyList<CandidateEvaluation>): IReadOnlyList<CandidateEvaluation>', '- BreakTies(ranked: IReadOnlyList<CandidateEvaluation>): TieBreakRecord?'] },
  Validator: { name: 'ConfigurationValidator', pal: 'rf10', a: [], m: ['+ Validate(cfg: AssignmentConfiguration,', '      catalog: IReadOnlyCollection<string>): ValidationResult'] },
  VResult: { name: 'ValidationResult', st: 'record', pal: 'vo', a: ['+ IsValid: bool', '+ Errors: IReadOnlyList<string>'], m: [] },

  IRule: { name: 'IAssignmentRule', st: 'interface', pal: 'port', a: ['+ Code: string { get; }', '+ Kind: RuleKind { get; }'], m: [] },
  IElim: { name: 'IEliminationRule', st: 'interface', pal: 'port', a: [], noAttrSection: true, m: ['+ Evaluate(ctx: AssignmentContext, c: Candidate,', '      s: RuleSetting): RuleOutcome'] },
  IPrio: { name: 'IPriorityRule', st: 'interface', pal: 'port', a: [], noAttrSection: true, m: ['+ Evaluate(ctx: AssignmentContext, c: Candidate,', '      s: RuleSetting): RuleOutcome'] },
  IScore: { name: 'IScoringRule', st: 'interface', pal: 'port', a: [], noAttrSection: true, m: ['+ Score(ctx: AssignmentContext, c: Candidate,', '      s: RuleSetting): RuleOutcome'] },
  ITie: { name: 'ITieBreakRule', st: 'interface', pal: 'port', a: [], noAttrSection: true, m: ['+ Compare(a: CandidateEvaluation,', '      b: CandidateEvaluation): int'] },

  Avail: { name: 'AvailabilityRule', pal: 'rf10', a: ['«sin asignación vigente que se solape»'], m: [] },
  Shift: { name: 'ShiftCompatibilityRule', pal: 'rf10', a: ['«turno que cubre la franja del servicio»'], m: [] },
  Reg: { name: 'RegulatoryRestrictionRule', pal: 'rf10', a: ['«licencia, RTM y SOAT vigentes»'], m: [] },
  Cap: { name: 'ServiceCapabilityRule', pal: 'rf10', a: ['«capacidad del vehículo ≥ pasajeros»'], m: [] },
  Crit: { name: 'CriticalServicePriorityRule', pal: 'rf10', a: ['«solicitud urgente (RF-13)»'], m: [] },
  TenantP: { name: 'TenantPriorityRule', pal: 'rf10', a: ['«prioridad definida por el operador»'], m: [] },
  Work: { name: 'WorkloadBalanceRule', pal: 'rf10', a: ['«menos servicios en el día ⇒ más puntos»'], m: [] },
  SvcComp: { name: 'ServiceCompatibilityRule', pal: 'rf10', a: ['«minimiza cambios de turno y vehículo»'], m: [] },
  Recent: { name: 'RecentAssignmentsRule', pal: 'rf10', a: ['«penaliza asignaciones recientes»'], m: [] },
  Stable: { name: 'StableTieBreaker', pal: 'rf10', a: ['1. menos asignaciones recientes', '2. mayor tiempo desde la última', '3. ConductorId (orden estable)'], m: [] },

  Ctx: { name: 'AssignmentContext', st: 'record', pal: 'vo', a: ['+ Request: RequestSnapshot', '+ Candidates: IReadOnlyList<Candidate>', '+ Configuration: AssignmentConfiguration', '+ EvaluatedAt: DateTimeOffset'], m: [] },
  Req: { name: 'RequestSnapshot', st: 'record', pal: 'vo', a: ['+ SolicitudId: Guid', '+ Franja: FranjaHoraria', '+ Pasajeros: int', '+ Urgente: bool', '+ EmpresaClienteId: Guid?'], m: [] },
  Cand: { name: 'Candidate', st: 'record', pal: 'vo', a: ['+ ConductorId: Guid', '+ VehiculoId: Guid', '+ VencimientoLicencia: DateOnly', '+ VencimientoRtm: DateOnly', '+ VencimientoSoat: DateOnly', '+ CapacidadVehiculo: int', '+ Turnos: IReadOnlyList<FranjaHoraria>', '+ AsignacionesVigentes: IReadOnlyList<FranjaHoraria>', '+ ServiciosEnElDia: int', '+ AsignacionesRecientes: int', '+ UltimaAsignacion: DateTimeOffset?'], m: [] },
  Cfg: { name: 'AssignmentConfiguration', st: 'record', pal: 'vo', a: ['+ ConfigurationId: Guid', '+ Version: int', '+ Rules: IReadOnlyList<RuleSetting>'], m: [] },
  Setting: { name: 'RuleSetting', st: 'record', pal: 'vo', a: ['+ Code: string', '+ Kind: RuleKind', '+ Enabled: bool', '+ Order: int', '+ Weight: decimal', '+ Parameters: IReadOnlyDictionary<string, string>'], m: [] },

  Result: { name: 'AssignmentResult', st: 'record', pal: 'vo', a: ['+ Outcome: AssignmentOutcome', '+ Selected: CandidateEvaluation?', '+ DecisionLog: AssignmentDecisionLog'], m: [] },
  Log: { name: 'AssignmentDecisionLog', pal: 'rf10', a: ['+ Id: Guid', '+ SolicitudId: Guid', '+ ConfigurationId: Guid', '+ ConfigurationVersion: int', '+ EvaluatedAt: DateTimeOffset', '+ Outcome: AssignmentOutcome', '+ SelectedConductorId: Guid?', '+ SelectedVehiculoId: Guid?', '+ InputSnapshot: string'], m: [] },
  Eval: { name: 'CandidateEvaluation', pal: 'rf10', a: ['+ ConductorId: Guid', '+ VehiculoId: Guid', '+ Eligible: bool', '+ Priority: int', '+ TotalScore: decimal', '+ Rank: int?'], m: [] },
  Outc: { name: 'RuleOutcome', st: 'record', pal: 'rf10', a: ['+ RuleCode: string', '+ Kind: RuleKind', '+ Order: int', '+ Passed: bool?', '+ RawValue: decimal?', '+ Weight: decimal?', '+ WeightedValue: decimal?', '+ Reason: string'], m: [] },
  Tie: { name: 'TieBreakRecord', st: 'record', pal: 'vo', a: ['+ TiedConductorIds: IReadOnlyList<Guid>', '+ DecidedBy: string', '+ WinnerConductorId: Guid'], m: [] },
  RuleKind: enumBox('RuleKind', ['Elimination', 'Priority', 'Scoring', 'TieBreak']),
  AssignOut: enumBox('AssignmentOutcome', ['Assigned', 'NoCandidates']),

  PSol: { name: 'ISolicitudViajeRepository', st: 'interface', pal: 'port', a: [], noAttrSection: true, m: ['+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<SolicitudViaje?>', '+ GetPendientesAsync(ct: CancellationToken): Task<IReadOnlyList<Guid>>', '+ AddAsignacion(a: Asignacion): void'] },
  PCand: { name: 'ICandidateProvider', st: 'interface', pal: 'port', a: [], noAttrSection: true, m: ['+ GetCandidatesAsync(s: SolicitudViaje, ct: CancellationToken):', '      Task<IReadOnlyList<Candidate>>'] },
  PCfg: { name: 'IAssignmentConfigurationProvider', st: 'interface', pal: 'port', a: [], noAttrSection: true, m: ['+ GetActiveAsync(ct: CancellationToken): Task<AssignmentConfiguration>'] },
  PLog: { name: 'IDecisionLogRepository', st: 'interface', pal: 'port', a: [], noAttrSection: true, m: ['+ Add(log: AssignmentDecisionLog): void'] },
  PUow: { name: 'IUnitOfWork', st: 'interface', pal: 'port', a: [], noAttrSection: true, m: ['+ SaveChangesAsync(ct: CancellationToken): Task'] },
  PTenant: { name: 'ITenantContext', st: 'interface', pal: 'port', a: [], noAttrSection: true, m: ['+ TenantId: string { get; }'] },
  PClock: { name: 'IClock', st: 'interface', pal: 'port', a: [], noAttrSection: true, m: ['+ UtcNow: DateTimeOffset { get; }'] },

  EfSol: { name: 'EfSolicitudViajeRepository', pal: 'infra', a: [], m: [] },
  EfCand: { name: 'EfCandidateProvider', pal: 'infra', a: [], m: [] },
  EfCfg: { name: 'EfAssignmentConfigurationProvider', pal: 'infra', a: [], m: [] },
  EfLog: { name: 'EfDecisionLogRepository', pal: 'infra', a: [], m: [] },
  Db: { name: 'FlowRideDbContext', pal: 'infra', a: [], m: ['+ SaveChangesAsync(ct: CancellationToken): Task'] },
  TenantRes: { name: 'TenantConnectionResolver', pal: 'infra', a: [], m: ['+ TenantId: string { get; }'] },
  SysClock: { name: 'SystemClock', pal: 'infra', a: [], m: ['+ UtcNow: DateTimeOffset { get; }'] },

  DSol: { name: 'SolicitudViaje', st: 'Domain', pal: 'dom', a: [], noAttrSection: true, m: [] },
  DAsig: { name: 'Asignacion', st: 'Domain', pal: 'dom', a: [], noAttrSection: true, m: [] },
};
const P2 = (k, x, y) => m.cls(k, x, y, M[k]);
const g2 = m.geo;
const PX = 40, PY = 140;

// Filas alineadas Infraestructura | Puerto
const rows = [['EfSol', 'PSol'], ['EfCand', 'PCand'], ['EfCfg', 'PCfg'], ['EfLog', 'PLog'], ['Db', 'PUow'], ['TenantRes', 'PTenant'], ['SysClock', 'PClock']];
const infraW = Math.max(...rows.map(([i]) => measure(M[i]).w));
const portsX = PX + infraW + 130;
let ry = PY;
for (const [i, p] of rows) {
  P2(i, PX, ry); P2(p, portsX, ry);
  const yy = ry + Math.min(g2[i].h, g2[p].h) / 2;
  m.edge(i, p, S.real, { ex: 1, ey: fy(m, i, yy), nx: 0, ny: fy(m, p, yy) });
  ry += Math.max(g2[i].h, g2[p].h) + 34;
}
const portsW = Math.max(...rows.map(([, p]) => g2[p].w));
m.frame(PX - 20, PY - 40, infraW + 40, ry - PY + 20, 'FlowRide.Infrastructure', '#FDEDEC');
m.frame(portsX - 20, PY - 40, portsW + 40, ry - PY + 20, 'FlowRide.Application · Abstractions (puertos)', '#FFF8EE');
// Repositorios EF → DbContext (canales escalonados a la izquierda)
['EfSol', 'EfCand', 'EfCfg', 'EfLog'].forEach((k, i) => {
  const xc = PX - 60 + 12 * i, ent = 0.8 - 0.2 * i;
  m.edge(k, 'Db', S.dep, { ex: 0, ey: 0.5, nx: 0, ny: ent, pts: [[xc, cy(m, k)], [xc, cy(m, 'Db', ent)]] });
});
m.edge('Db', 'PTenant', S.dep, { label: '«usa»', ex: 1, ey: 0.85, nx: 0, ny: 0.25, pts: [[portsX - 35, cy(m, 'Db', 0.85)], [portsX - 35, cy(m, 'PTenant', 0.25)]] });

// Caso de uso, motor y validador
const xC = portsX + portsW + 200;
P2('Handler', xC, PY);
P2('Engine', xC, g2.Handler.y + g2.Handler.h + 90);
P2('DSol', g2.Handler.x + g2.Handler.w + 80, PY + 30);
P2('DAsig', g2.DSol.x + g2.DSol.w + 40, PY + 30);
P2('Validator', g2.Engine.x + g2.Engine.w + 70, g2.Engine.y + g2.Engine.h + 40);
P2('VResult', g2.Validator.x + g2.Validator.w + 50, g2.Validator.y);
// Bus caso de uso → puertos (sin cruces: filas inferiores usan canales más cercanos al handler)
['PSol', 'PCand', 'PCfg', 'PLog', 'PUow', 'PClock'].forEach((k, i) => {
  const xc = portsX + portsW + 40 + 22 * i, ent = 0.16 + i * 0.14;
  m.edge('Handler', k, S.assoc, { ex: 0, ey: ent, nx: 1, ny: 0.5, pts: [[xc, cy(m, 'Handler', ent)], [xc, cy(m, k)]] });
});
m.edge('Handler', 'Engine', S.assoc, { label: 'usa', ex: fx(m, 'Handler', xC + 120), ey: 1, nx: fx(m, 'Engine', xC + 120), ny: 0 });
m.edge('Handler', 'DSol', S.dep, { label: '«modifica»', ex: 1, ey: fy(m, 'Handler', cy(m, 'DSol')), nx: 0, ny: 0.5 });
m.edge('Handler', 'DAsig', S.dep, { label: '«crea»', ex: 1, ey: fy(m, 'Handler', g2.DSol.y + g2.DSol.h + 45), nx: 0.5, ny: 1, pts: [[cx(m, 'DAsig'), g2.DSol.y + g2.DSol.h + 45]] });
m.edge('Engine', 'Validator', S.assoc, { ex: fx(m, 'Engine', g2.Engine.x + g2.Engine.w - 50), ey: 1, nx: 0, ny: 0.5, pts: [[g2.Engine.x + g2.Engine.w - 50, cy(m, 'Validator')]] });
m.edge('Validator', 'VResult', S.dep, { label: '«produce»', ex: 1, ey: fy(m, 'Validator', cy(m, 'VResult')), nx: 0, ny: 0.5 });

// Columna D: contexto (arriba) y resultado + decision log (abajo)
const xD = Math.max(rightOf(m, ['DAsig', 'VResult', 'Validator']), g2.Engine.x + g2.Engine.w) + 170;
P2('Ctx', xD, PY);
// Columna E (se calcula antes para ubicar Result debajo de Candidate)
const wD = Math.max(...['Ctx', 'Result', 'Log', 'Tie'].map(k => measure(M[k]).w));
const xE = xD + wD + 150;
P2('Req', xE, PY);
P2('Cand', xE, g2.Req.y + g2.Req.h + 50);
const yRes = Math.max(g2.Ctx.y + g2.Ctx.h + 60, g2.Cand.y + g2.Cand.h + 50);
P2('Result', xD, yRes);
P2('AssignOut', xE, yRes);
P2('Log', xD, g2.Result.y + g2.Result.h + 60);
P2('Tie', xD, g2.Log.y + g2.Log.h + 60);
P2('Eval', xE, Math.max(g2.AssignOut.y + g2.AssignOut.h + 50, g2.Log.y + 40));
const wE = Math.max(...['Req', 'Cand', 'AssignOut', 'Eval'].map(k => g2[k].w));
const xF = xE + wE + 150;
P2('Cfg', xF, PY);
P2('Setting', xF, g2.Cfg.y + g2.Cfg.h + 50);
P2('RuleKind', xF, g2.Setting.y + g2.Setting.h + 50);
P2('Outc', xF, Math.max(g2.RuleKind.y + g2.RuleKind.h + 50, g2.Eval.y));

// Engine consume/produce (sin cruces)
m.edge('Engine', 'Ctx', S.dep, { label: '«consume»', ex: 1, ey: 0.12, nx: 0, ny: 0.5, pts: [[xD - 45, cy(m, 'Engine', 0.12)], [xD - 45, cy(m, 'Ctx')]] });
{
  const yp = Math.min(cy(m, 'Result'), g2.Engine.y + g2.Engine.h - 15);
  m.edge('Engine', 'Result', S.dep, { label: '«produce»', ex: 1, ey: fy(m, 'Engine', yp), nx: 0, ny: fy(m, 'Result', yp), pts: yp === cy(m, 'Result') ? undefined : [[xD - 80, yp], [xD - 80, cy(m, 'Result')]] });
}
m.edge('Ctx', 'Req', S.comp, { s: '1', t: '1', ex: 1, ey: fy(m, 'Ctx', PY + 30), nx: 0, ny: fy(m, 'Req', PY + 30) });
m.edge('Ctx', 'Cand', S.comp, { s: '1', t: '0..*', ex: 1, ey: 0.8, nx: 0, ny: 0.5, pts: [[xE - 45, cy(m, 'Ctx', 0.8)], [xE - 45, cy(m, 'Cand')]] });
m.edge('Ctx', 'Cfg', S.assoc, { t: '1', ex: 0.5, ey: 0, nx: 0.5, ny: 0, pts: [[cx(m, 'Ctx'), PY - 30], [cx(m, 'Cfg'), PY - 30]] });
m.edge('Cfg', 'Setting', S.comp, { s: '1', t: '1..*', ex: fx(m, 'Cfg', xF + 60), ey: 1, nx: fx(m, 'Setting', xF + 60), ny: 0 });
m.edge('Setting', 'RuleKind', S.dep, { ex: fx(m, 'Setting', cx(m, 'RuleKind')), ey: 1, nx: 0.5, ny: 0 });
m.edge('Outc', 'RuleKind', S.dep, { ex: fx(m, 'Outc', cx(m, 'RuleKind')), ey: 0, nx: 0.5, ny: 1 });
m.edge('Result', 'Log', S.comp, { s: '1', t: '1', ex: fx(m, 'Result', xD + 60), ey: 1, nx: fx(m, 'Log', xD + 60), ny: 0 });
m.edge('Result', 'AssignOut', S.dep, { ex: 1, ey: fy(m, 'Result', cy(m, 'AssignOut')), nx: 0, ny: 0.5 });
m.edge('Log', 'AssignOut', S.dep, { ex: 1, ey: fy(m, 'Log', g2.Log.y + 18), nx: 0.5, ny: 1, pts: [[cx(m, 'AssignOut'), g2.Log.y + 18]] });
{
  const ye = Math.max(g2.Eval.y + 30, g2.Log.y + 50);
  m.edge('Log', 'Eval', S.comp, { s: '1', t: '0..*', ex: 1, ey: fy(m, 'Log', ye), nx: 0, ny: fy(m, 'Eval', ye) });
  const yo = g2.Outc.y + Math.min(g2.Outc.h, g2.Eval.h) / 2;
  m.edge('Eval', 'Outc', S.comp, { s: '1', t: '0..*', ex: 1, ey: fy(m, 'Eval', yo), nx: 0, ny: fy(m, 'Outc', yo) });
}
m.edge('Log', 'Tie', S.comp, { s: '1', t: '0..1', ex: fx(m, 'Log', xD + 60), ey: 1, nx: fx(m, 'Tie', xD + 60), ny: 0 });

// Interfaz de reglas y banda de reglas concretas
const yIR = Math.max(bottomOf(m, ['Validator', 'VResult', 'Engine']) + 80);
const irW = measure(M.IRule).w;
P2('IRule', cx(m, 'Engine', 0.4) - irW / 2, yIR);
m.edge('Engine', 'IRule', S.aggr, { s: '1', t: '1..*  {catálogo}', ex: 0.4, ey: 1, nx: 0.5, ny: 0 });
const ruleCols = [
  ['IElim', ['Avail', 'Shift', 'Reg', 'Cap'], 'Reglas eliminatorias'],
  ['IPrio', ['Crit', 'TenantP'], 'Reglas de prioridad'],
  ['IScore', ['Work', 'SvcComp', 'Recent'], 'Reglas de puntuación'],
  ['ITie', ['Stable'], 'Reglas de desempate'],
];
const ryTop = Math.max(g2.IRule.y + g2.IRule.h + 100, bottomOf(m, ['Tie', 'Outc', 'Eval']) + 90);
let rx = xC - 10, ruleBottom = 0;
for (const [ik, impls, title] of ruleCols) {
  const gi = P2(ik, rx, ryTop);
  let yy = gi.y + gi.h + 50, colW = gi.w;
  for (const k of impls) { const g = P2(k, rx + 24, yy); colW = Math.max(colW, g.w + 24); yy += g.h + 18; }
  m.frame(rx - 14, ryTop - 36, colW + 28, yy - ryTop + 30, title, '#FFF5E6');
  for (const k of impls) m.edge(k, ik, S.real, { ex: 0, ey: 0.5, nx: fx(m, ik, rx + 10), ny: 1, pts: [[rx + 10, cy(m, k)]] });
  m.edge(ik, 'IRule', S.inh, { ex: 0.5, ey: 0, nx: 0.5, ny: 1, pts: [[cx(m, ik), ryTop - 55], [cx(m, 'IRule'), ryTop - 55]] });
  ruleBottom = Math.max(ruleBottom, yy);
  rx += colW + 70;
}

{
  const yB = Math.max(ruleBottom, ry) + 60;
  legend(m, PX, yB, [
    ['rf10', 'Motor de asignación (RF-10)'],
    ['port', 'Interfaz (contrato)'],
    ['vo', 'Record inmutable de entrada/salida'],
    ['enum', 'Enumeración'],
    ['dom', 'Clase del dominio (página 1)'],
    ['infra', 'Implementación en Infrastructure'],
  ]);
  m.note(PX + 420, yB, 760, 215,
    '<b>Flujo determinista de AssignmentEngine.Decide(ctx)</b><br/>' +
    '1. Candidatos ordenados por ConductorId (ninguna consulta sin ORDER BY).<br/>' +
    '2. ConfigurationValidator valida la configuración; si es inválida no se asigna.<br/>' +
    '3. Reglas <b>eliminatorias</b> por Order: los descartados quedan en el log con su motivo.<br/>' +
    '4. Reglas de <b>prioridad</b>: nivel de prioridad de cada candidato.<br/>' +
    '5. Reglas de <b>puntuación</b>: valor 0..100 × Weight; TotalScore = Σ.<br/>' +
    '6. Ranking: Priority desc, TotalScore desc.<br/>' +
    '7. <b>Desempate</b> estable (StableTieBreaker), nunca aleatorio.<br/>' +
    '8. AssignmentResult + AssignmentDecisionLog: candidatos, resultado de cada regla, puntajes, desempate y versión de configuración.<br/>' +
    'Misma entrada + misma configuración ⇒ mismo resultado. El tiempo llega por IClock, nunca DateTime.Now.');
  m.note(PX + 1210, yB, 760, 215,
    '<b>Persistencia y consistencia (ADR-0007)</b><br/>' +
    '• El caso de uso guarda Asignacion, el cambio de estado de SolicitudViaje y el AssignmentDecisionLog en <b>una sola transacción</b> (IUnitOfWork).<br/>' +
    '• Concurrencia optimista con token de versión en SolicitudViaje, Asignacion, Turno y Vehiculo.<br/>' +
    '• La base de datos impide dos asignaciones vigentes por solicitud y franjas solapadas por conductor o vehículo (página 3).<br/>' +
    '• Ante un conflicto se recargan los candidatos y se reintenta la decisión un número acotado de veces; sin candidatos ⇒ SolicitudViaje.SinCandidatos y alerta al Operador logístico (RF-13).<br/>' +
    '• Las reglas no se conocen entre sí; solo el motor las orquesta. Una regla nueva = una clase nueva registrada en el catálogo (inyección de dependencias).');
}

// =====================================================================
// PÁGINA 3 — MODELO ENTIDAD-RELACIÓN (PostgreSQL 17, BD por operador)
// =====================================================================
const r = new Page('3. Entidad-Relación');
header(r, 'FlowRide — Modelo entidad-relación', `PostgreSQL 17 · una base de datos por operador (Silo) · esquemas ops, motor, telemetria, notif y audit · ${SUB}`);

const C = { ops: '#3B6EA8', motor: '#C77700', tel: '#4E8A3E', notif: '#7A5C99', audit: '#555555', plat: '#A33A3A' };
const T = {
  rol: { name: 'ops.rol', color: C.ops, cols: [['PK', 'id', 'uuid'], ['UK', 'nombre', 'varchar(50)'], ['', 'descripcion', 'varchar(200)']] },
  usuario: { name: 'ops.usuario', color: C.ops, cols: [['PK', 'id', 'uuid'], ['FK', 'rol_id', 'uuid'], ['', 'nombre', 'varchar(150)'], ['UK', 'correo', 'varchar(150) NULL'], ['UK', 'telefono_whatsapp', 'varchar(20)'], ['UK', 'identidad_externa_id', 'varchar(100) NULL'], ['', 'activo', 'boolean'], ['', 'creado_en', 'timestamptz'], ['', 'xmin', 'xid  «versión»']] },
  pasajero: { name: 'ops.pasajero', color: C.ops, cols: [['PK,FK', 'usuario_id', 'uuid'], ['FK', 'empresa_cliente_id', 'uuid'], ['', 'codigo_interno', 'varchar(30)']], cons: ['UQ (empresa_cliente_id, codigo_interno)'] },
  empresa: { name: 'ops.empresa_cliente', color: C.ops, cols: [['PK', 'id', 'uuid'], ['', 'nombre', 'varchar(150)'], ['UK', 'nit', 'varchar(20)'], ['', 'correo_contacto', 'varchar(150)'], ['', 'activa', 'boolean']] },
  itinerario: { name: 'ops.itinerario', color: C.ops, cols: [['PK', 'id', 'uuid'], ['FK', 'empresa_cliente_id', 'uuid'], ['', 'codigo_servicio', 'varchar(30)'], ['', 'fecha', 'date'], ['', 'origen', 'varchar(200)'], ['', 'destino', 'varchar(200)'], ['', 'hora_salida', 'time'], ['', 'hora_llegada', 'time']], cons: ['UQ (empresa_cliente_id, codigo_servicio, fecha)'] },
  parametro: { name: 'ops.parametro', color: C.ops, cols: [['PK', 'clave', 'varchar(100)'], ['', 'tipo', 'varchar(20)'], ['', 'valor', 'jsonb'], ['', 'version', 'integer'], ['FK', 'modificado_por_id', 'uuid'], ['', 'modificado_en', 'timestamptz']], cons: ['Catálogo de parámetros (ADR-0016)'] },

  conductor: { name: 'ops.conductor', color: C.ops, cols: [['PK,FK', 'usuario_id', 'uuid'], ['UK', 'numero_licencia', 'varchar(30)'], ['', 'categoria_licencia', 'varchar(5)'], ['', 'vencimiento_licencia', 'date'], ['', 'estado', 'varchar(20)'], ['', 'fecha_ultima_asignacion', 'timestamptz NULL'], ['', 'xmin', 'xid  «versión»']] },
  documento: { name: 'ops.documento_conductor', color: C.ops, cols: [['PK', 'id', 'uuid'], ['FK', 'conductor_id', 'uuid'], ['', 'tipo', 'varchar(20)'], ['', 'referencia_blob', 'varchar(300)'], ['', 'fecha_vencimiento', 'date NULL']] },
  turno: { name: 'ops.turno', color: C.ops, cols: [['PK', 'id', 'uuid'], ['FK', 'conductor_id', 'uuid'], ['', 'inicio', 'timestamptz'], ['', 'fin', 'timestamptz'], ['', 'estado', 'varchar(20)'], ['', 'xmin', 'xid  «versión»']], cons: ['CHECK (fin > inicio)', 'EXCLUDE gist (conductor_id =, tstzrange(inicio,fin) &&)'] },
  vehiculo: { name: 'ops.vehiculo', color: C.ops, cols: [['PK', 'id', 'uuid'], ['UK', 'placa', 'varchar(10)'], ['', 'marca', 'varchar(50)'], ['', 'modelo', 'varchar(50)'], ['', 'capacidad', 'smallint'], ['', 'activo', 'boolean'], ['', 'vencimiento_rtm', 'date'], ['', 'vencimiento_soat', 'date'], ['FK,UK', 'conductor_habitual_id', 'uuid NULL'], ['', 'xmin', 'xid  «versión»']], cons: ['CHECK (capacidad > 0)'] },

  solicitud: { name: 'ops.solicitud_viaje', color: C.ops, cols: [['PK', 'id', 'uuid'], ['FK', 'pasajero_id', 'uuid'], ['FK', 'empresa_cliente_id', 'uuid NULL'], ['FK', 'itinerario_id', 'uuid NULL'], ['', 'direccion_origen', 'varchar(200)'], ['', 'direccion_destino', 'varchar(200)'], ['', 'fecha_hora_servicio', 'timestamptz'], ['', 'duracion_estimada', 'interval'], ['', 'cantidad_pasajeros', 'smallint'], ['', 'urgente', 'boolean'], ['', 'es_personal', 'boolean'], ['', 'canal', 'varchar(20)'], ['', 'estado', 'varchar(20)'], ['', 'registrada_en', 'timestamptz'], ['', 'xmin', 'xid  «versión»']], cons: ['IX (estado, fecha_hora_servicio)', 'CHECK (cantidad_pasajeros > 0)'] },
  historial: { name: 'ops.estado_solicitud_historial', color: C.ops, cols: [['PK', 'id', 'uuid'], ['FK', 'solicitud_id', 'uuid'], ['', 'estado_anterior', 'varchar(20) NULL'], ['', 'estado_nuevo', 'varchar(20)'], ['', 'fecha_hora', 'timestamptz'], ['FK', 'actor_id', 'uuid NULL'], ['', 'motivo', 'text NULL']] },
  cancelacion: { name: 'ops.cancelacion_solicitud', color: C.ops, cols: [['PK', 'id', 'uuid'], ['FK,UK', 'solicitud_id', 'uuid'], ['', 'motivo', 'text'], ['', 'fecha_hora', 'timestamptz'], ['FK', 'cancelada_por_id', 'uuid']] },

  asignacion: { name: 'ops.asignacion', color: C.ops, cols: [['PK', 'id', 'uuid'], ['FK', 'solicitud_id', 'uuid'], ['FK', 'conductor_id', 'uuid'], ['FK', 'vehiculo_id', 'uuid'], ['FK', 'decision_id', 'uuid NULL'], ['FK', 'asignacion_reemplazada_id', 'uuid NULL'], ['', 'origen', 'varchar(20)'], ['', 'estado', 'varchar(20)'], ['', 'franja', 'tstzrange'], ['', 'asignada_en', 'timestamptz'], ['', 'xmin', 'xid  «versión»']], cons: ['UQ parcial (solicitud_id) WHERE vigente', 'EXCLUDE gist (conductor_id =, franja &&) WHERE vigente', 'EXCLUDE gist (vehiculo_id =, franja &&) WHERE vigente', 'vigente = estado IN (Asignada, Aceptada, EnCurso)'] },
  viaje: { name: 'ops.viaje', color: C.ops, cols: [['PK', 'id', 'uuid'], ['FK,UK', 'asignacion_id', 'uuid'], ['', 'estado', 'varchar(20)'], ['', 'eta_programada', 'timestamptz'], ['', 'fecha_hora_inicio', 'timestamptz NULL'], ['', 'fecha_hora_fin', 'timestamptz NULL'], ['', 'xmin', 'xid  «versión»']] },
  parada: { name: 'ops.parada', color: C.ops, cols: [['PK', 'id', 'uuid'], ['FK', 'viaje_id', 'uuid'], ['', 'orden', 'smallint'], ['', 'direccion', 'varchar(200)'], ['', 'hora_estimada', 'timestamptz'], ['', 'hora_real', 'timestamptz NULL']], cons: ['UQ (viaje_id, orden)'] },
  incidente: { name: 'ops.incidente', color: C.ops, cols: [['PK', 'id', 'uuid'], ['FK', 'viaje_id', 'uuid'], ['', 'tipo', 'varchar(20)'], ['', 'descripcion', 'text'], ['', 'fecha_hora', 'timestamptz'], ['FK', 'reportado_por_id', 'uuid']] },
  calificacion: { name: 'ops.calificacion', color: C.ops, cols: [['PK', 'id', 'uuid'], ['FK', 'viaje_id', 'uuid'], ['FK', 'pasajero_id', 'uuid'], ['FK', 'conductor_id', 'uuid'], ['', 'puntaje', 'smallint'], ['', 'comentario', 'text NULL'], ['', 'fecha_hora', 'timestamptz']], cons: ['UQ (viaje_id, pasajero_id)', 'CHECK (puntaje BETWEEN 1 AND 5)'] },

  config: { name: 'motor.configuracion_asignacion', color: C.motor, cols: [['PK', 'id', 'uuid'], ['UK', 'version', 'integer'], ['', 'estado', 'varchar(20)'], ['FK', 'creada_por_id', 'uuid'], ['', 'creada_en', 'timestamptz'], ['', 'activada_en', 'timestamptz NULL']], cons: ['UQ parcial (estado) WHERE estado = \'Activa\''] },
  regla: { name: 'motor.regla_configurada', color: C.motor, cols: [['PK', 'id', 'uuid'], ['FK', 'configuracion_id', 'uuid'], ['', 'codigo_regla', 'varchar(60)'], ['', 'tipo', 'varchar(20)'], ['', 'habilitada', 'boolean'], ['', 'orden', 'smallint'], ['', 'peso', 'numeric(5,4)'], ['', 'parametros', 'jsonb']], cons: ['UQ (configuracion_id, codigo_regla)', 'UQ (configuracion_id, tipo, orden)', 'CHECK (peso BETWEEN 0 AND 1)'] },
  decision: { name: 'motor.decision_asignacion', color: C.motor, cols: [['PK', 'id', 'uuid'], ['FK', 'solicitud_id', 'uuid'], ['FK', 'configuracion_id', 'uuid'], ['', 'version_configuracion', 'integer'], ['', 'evaluada_en', 'timestamptz'], ['', 'resultado', 'varchar(20)'], ['FK', 'conductor_seleccionado_id', 'uuid NULL'], ['FK', 'vehiculo_seleccionado_id', 'uuid NULL'], ['', 'desempate', 'jsonb NULL'], ['', 'entrada_snapshot', 'jsonb']], cons: ['Inmutable (solo INSERT)'] },
  evaluacion: { name: 'motor.evaluacion_candidato', color: C.motor, cols: [['PK', 'id', 'uuid'], ['FK', 'decision_id', 'uuid'], ['FK', 'conductor_id', 'uuid'], ['FK', 'vehiculo_id', 'uuid'], ['', 'elegible', 'boolean'], ['', 'prioridad', 'integer'], ['', 'puntaje_total', 'numeric(9,4)'], ['', 'posicion_ranking', 'integer NULL']], cons: ['UQ (decision_id, conductor_id)'] },
  resultado: { name: 'motor.resultado_regla', color: C.motor, cols: [['PK', 'id', 'uuid'], ['FK', 'evaluacion_id', 'uuid'], ['', 'codigo_regla', 'varchar(60)'], ['', 'tipo', 'varchar(20)'], ['', 'orden', 'smallint'], ['', 'aprobado', 'boolean NULL'], ['', 'valor', 'numeric(9,4) NULL'], ['', 'peso', 'numeric(5,4) NULL'], ['', 'valor_ponderado', 'numeric(9,4) NULL'], ['', 'motivo', 'text']], cons: ['UQ (evaluacion_id, codigo_regla)'] },

  ubicacion: { name: 'telemetria.registro_ubicacion', color: C.tel, cols: [['PK', 'id', 'bigint identity'], ['PK', 'timestamp_origen', 'timestamptz'], ['FK', 'vehiculo_id', 'uuid'], ['FK', 'conductor_id', 'uuid'], ['FK', 'viaje_id', 'uuid NULL'], ['', 'timestamp_recepcion', 'timestamptz'], ['', 'latitud', 'numeric(9,6)'], ['', 'longitud', 'numeric(9,6)'], ['', 'calidad', 'varchar(20) NULL'], ['', 'fuente', 'varchar(30)']], cons: ['PARTITION BY RANGE (timestamp_origen) mensual', 'Retención 6 meses (DROP de partición)', 'IX (viaje_id, timestamp_origen)'] },
  desviacion: { name: 'telemetria.desviacion_eta', color: C.tel, cols: [['PK', 'id', 'uuid'], ['FK', 'viaje_id', 'uuid'], ['', 'detectada_en', 'timestamptz'], ['', 'eta_estimada', 'timestamptz'], ['', 'minutos_desviacion', 'integer'], ['', 'estado', 'varchar(20)']] },

  outbox: { name: 'notif.outbox_mensaje', color: C.notif, cols: [['PK', 'id', 'uuid'], ['UK', 'event_id', 'uuid'], ['', 'tipo_evento', 'varchar(80)'], ['', 'payload', 'jsonb'], ['', 'ocurrido_en', 'timestamptz'], ['', 'proximo_intento', 'timestamptz'], ['', 'intentos', 'smallint'], ['', 'procesado_en', 'timestamptz NULL'], ['', 'ultimo_error', 'text NULL']], cons: ['IX parcial (proximo_intento) WHERE procesado_en IS NULL'] },
  notificacion: { name: 'notif.notificacion_enviada', color: C.notif, cols: [['PK', 'id', 'uuid'], ['FK', 'event_id', 'uuid'], ['FK', 'destinatario_id', 'uuid'], ['', 'canal', 'varchar(20)'], ['', 'clave_mensaje', 'varchar(100)'], ['', 'estado', 'varchar(20)'], ['', 'enviado_en', 'timestamptz NULL']], cons: ['UQ (event_id, destinatario_id)  «idempotencia»'] },
  plantilla: { name: 'notif.plantilla_whatsapp', color: C.notif, cols: [['PK', 'id', 'uuid'], ['', 'clave_mensaje', 'varchar(100)'], ['', 'idioma', 'varchar(10)'], ['', 'nombre_plantilla', 'varchar(100)'], ['', 'variables', 'jsonb'], ['', 'estado_aprobacion', 'varchar(20)']], cons: ['UQ (clave_mensaje, idioma)'] },
  conversacion: { name: 'notif.conversacion_whatsapp', color: C.notif, cols: [['PK', 'id', 'uuid'], ['FK,UK', 'usuario_id', 'uuid'], ['', 'estado_flujo', 'varchar(40)'], ['', 'datos_parciales', 'jsonb'], ['', 'ultima_interaccion', 'timestamptz']], cons: ['Ventana de 24 h = ultima_interaccion'] },

  auditoria: { name: 'audit.evento_auditoria', color: C.audit, cols: [['PK', 'id', 'bigint identity'], ['', 'actor_id', 'uuid NULL'], ['', 'accion', 'varchar(80)'], ['', 'recurso', 'varchar(80)'], ['', 'recurso_id', 'varchar(64)'], ['', 'fecha_hora', 'timestamptz'], ['', 'resultado', 'varchar(20)'], ['', 'motivo', 'text NULL'], ['', 'valores_antes', 'jsonb NULL'], ['', 'valores_despues', 'jsonb NULL'], ['', 'hash_previo', 'char(64)'], ['', 'hash', 'char(64)']], cons: ['Solo INSERT · retención 5 años', 'Sin FK: sobrevive a la supresión de datos'] },

  operador: { name: 'plataforma.operador_tenant', color: C.plat, w: 340, cols: [['PK', 'id', 'varchar(40)  «claim tenant_id»'], ['', 'razon_social', 'varchar(150)'], ['UK', 'nit', 'varchar(20)'], ['', 'correo_contacto', 'varchar(150)'], ['', 'estado', 'varchar(20)'], ['', 'ref_secreto_conexion', 'varchar(200)'], ['', 'creado_en', 'timestamptz']], cons: ['BD de plataforma, fuera de los Silos', 'Sin datos operacionales'] },
};

const RY = 130;
columns(r, 40, RY, [
  [['rol', T.rol], ['usuario', T.usuario], ['pasajero', T.pasajero], ['empresa', T.empresa], ['itinerario', T.itinerario], ['parametro', T.parametro]],
  [['solicitud', T.solicitud], ['historial', T.historial], ['cancelacion', T.cancelacion]],
  [['asignacion', T.asignacion], ['conductor', T.conductor], ['turno', T.turno], ['documento', T.documento], ['vehiculo', T.vehiculo]],
  [['viaje', T.viaje], ['parada', T.parada], ['incidente', T.incidente], ['calificacion', T.calificacion], ['desviacion', T.desviacion], ['ubicacion', T.ubicacion]],
  [['decision', T.decision], ['evaluacion', T.evaluacion], ['resultado', T.resultado], ['config', T.config], ['regla', T.regla]],
  [['outbox', T.outbox], ['notificacion', T.notificacion], ['conversacion', T.conversacion], ['plantilla', T.plantilla]],
  [['auditoria', T.auditoria], ['operador', T.operador]],
], 150, 50, 'tbl');

// child -> parent ; startArrow = cardinalidad del lado hijo, endArrow = lado padre
const ER = { many: 'ERmany', zmany: 'ERzeroToMany', omany: 'ERoneToMany', one: 'ERmandOne', zone: 'ERzeroToOne' };
const rel = (child, parent, cs, ps, o = {}) => r.edge(child, parent, `startArrow=${ER[cs]};endArrow=${ER[ps]};startFill=0;endFill=0;startSize=10;endSize=10;`, o);
const G = r.geo, mid = (k, f = 0.5) => G[k].y + G[k].h * f, right = keys => Math.max(...keys.map(k => G[k].x + G[k].w));
const c2x = G.solicitud.x, c3x = G.asignacion.x, c4x = G.viaje.x;
const c2r = right(['solicitud', 'historial', 'cancelacion']);
const c3r = right(['asignacion', 'conductor', 'turno', 'documento', 'vehiculo']);
const c4r = right(['viaje', 'parada', 'incidente', 'calificacion', 'desviacion', 'ubicacion']);
const c5r = right(['decision', 'evaluacion', 'resultado', 'config', 'regla']);

rel('usuario', 'rol', 'zmany', 'one');
rel('pasajero', 'usuario', 'zone', 'one');
rel('pasajero', 'empresa', 'zmany', 'one');
rel('itinerario', 'empresa', 'zmany', 'one', { ex: 0.5, ey: 0, nx: 0.5, ny: 1 });
rel('conductor', 'usuario', 'zone', 'one', { ex: 0, ey: 0.15, nx: 1, ny: 0.4, pts: [[c3x - 35, mid('conductor', 0.15)], [c3x - 35, RY - 30], [c2x - 35, RY - 30], [c2x - 35, mid('usuario', 0.4)]] });
rel('solicitud', 'pasajero', 'zmany', 'one', { ex: 0, ey: 0.55, nx: 1, ny: 0.5, pts: [[c2x - 60, mid('solicitud', 0.55)], [c2x - 60, mid('pasajero')]] });
rel('solicitud', 'empresa', 'zmany', 'zone', { ex: 0, ey: 0.7, nx: 1, ny: 0.5, pts: [[c2x - 80, mid('solicitud', 0.7)], [c2x - 80, mid('empresa')]] });
rel('solicitud', 'itinerario', 'zmany', 'zone', { ex: 0, ey: 0.85, nx: 1, ny: 0.5, pts: [[c2x - 100, mid('solicitud', 0.85)], [c2x - 100, mid('itinerario')]] });
rel('historial', 'solicitud', 'omany', 'one', { ex: 0.5, ey: 0, nx: 0.5, ny: 1 });
rel('cancelacion', 'solicitud', 'zone', 'one', { ex: 1, ey: 0.5, nx: 1, ny: 0.9, pts: [[c2r + 25, mid('cancelacion')], [c2r + 25, mid('solicitud', 0.9)]] });
rel('asignacion', 'solicitud', 'zmany', 'one', { ex: 0, ey: 0.15, nx: 1, ny: 0.15 });
rel('asignacion', 'conductor', 'zmany', 'one', { ex: 0.5, ey: 1, nx: 0.5, ny: 0 });
rel('asignacion', 'vehiculo', 'zmany', 'one', { ex: 0, ey: 0.6, nx: 0, ny: 0.5, pts: [[c3x - 15, mid('asignacion', 0.6)], [c3x - 15, mid('vehiculo')]] });
rel('turno', 'conductor', 'zmany', 'one', { ex: 1, ey: 0.5, nx: 1, ny: 0.4, pts: [[c3r + 15, mid('turno')], [c3r + 15, mid('conductor', 0.4)]] });
rel('documento', 'conductor', 'zmany', 'one', { ex: 1, ey: 0.5, nx: 1, ny: 0.6, pts: [[c3r + 30, mid('documento')], [c3r + 30, mid('conductor', 0.6)]] });
rel('vehiculo', 'conductor', 'zone', 'zone', { label: 'habitual', ex: 1, ey: 0.3, nx: 1, ny: 0.8, pts: [[c3r + 45, mid('vehiculo', 0.3)], [c3r + 45, mid('conductor', 0.8)]] });
rel('viaje', 'asignacion', 'zone', 'one', { ex: 0, ey: 0.3, nx: 1, ny: 0.3, pts: [[c3r + 70, mid('viaje', 0.3)], [c3r + 70, mid('asignacion', 0.3)]] });
const kids = [['parada', 'zmany', 'one', 0.25], ['incidente', 'zmany', 'one', 0.4], ['calificacion', 'zmany', 'one', 0.55], ['desviacion', 'zmany', 'one', 0.7], ['ubicacion', 'zmany', 'zone', 0.85]];
kids.forEach(([k, cs, ps, f], i) => rel(k, 'viaje', cs, ps, { ex: 1, ey: 0.5, nx: 1, ny: f, pts: [[c4r + 15 + 15 * i, mid(k)], [c4r + 15 + 15 * i, mid('viaje', f)]] }));
rel('ubicacion', 'vehiculo', 'zmany', 'one', { ex: 0, ey: 0.5, nx: 1, ny: 0.5, pts: [[c4x - 25, mid('ubicacion')], [c4x - 25, mid('vehiculo')]] });
rel('asignacion', 'decision', 'zone', 'zone', { ex: 0.8, ey: 0, nx: 0.3, ny: 0, pts: [[G.asignacion.x + G.asignacion.w * 0.8, RY - 30], [G.decision.x + G.decision.w * 0.3, RY - 30]] });
rel('decision', 'solicitud', 'zmany', 'one', { ex: 0.6, ey: 0, nx: 0.5, ny: 0, pts: [[G.decision.x + G.decision.w * 0.6, RY - 55], [G.solicitud.x + G.solicitud.w * 0.5, RY - 55]] });
rel('decision', 'config', 'zmany', 'one', { ex: 1, ey: 0.85, nx: 1, ny: 0.3, pts: [[c5r + 25, mid('decision', 0.85)], [c5r + 25, mid('config', 0.3)]] });
rel('evaluacion', 'decision', 'zmany', 'one', { ex: 0.5, ey: 0, nx: 0.5, ny: 1 });
rel('resultado', 'evaluacion', 'zmany', 'one', { ex: 0.5, ey: 0, nx: 0.5, ny: 1 });
rel('regla', 'config', 'omany', 'one', { ex: 0.5, ey: 0, nx: 0.5, ny: 1 });
rel('asignacion', 'asignacion', 'zone', 'zone', { label: 'reemplaza', ex: 1, ey: 0.55, nx: 1, ny: 0.8, pts: [[G.asignacion.x + G.asignacion.w + 28, mid('asignacion', 0.55)], [G.asignacion.x + G.asignacion.w + 28, mid('asignacion', 0.8)]] });
rel('notificacion', 'outbox', 'zmany', 'one', { ex: 0.5, ey: 0, nx: 0.5, ny: 1 });

{
  const yB = Math.max(...Object.values(r.geo).map(g => g.y + g.h)) + 50;
  let ly = yB;
  r.raw(`<mxCell id="${r.id()}" value="&lt;b&gt;Esquemas&lt;/b&gt;" style="text;html=1;fontSize=12;" vertex="1" parent="1"><mxGeometry x="40" y="${ly}" width="200" height="20" as="geometry"/></mxCell>`);
  ly += 24;
  for (const [c, t] of [[C.ops, 'ops — operación (RF-03..RF-34, RF-54)'], [C.motor, 'motor — configuración y decision log del motor (RF-10/11/21)'], [C.tel, 'telemetria — GPS y desviación de ETA (RF-55/56)'], [C.notif, 'notif — outbox, notificaciones y WhatsApp'], [C.audit, 'audit — auditoría legal (ADR-0011)'], [C.plat, 'plataforma — registro de tenants (BD aparte)']]) {
    r.raw(`<mxCell id="${r.id()}" value="" style="rounded=0;fillColor=${c};strokeColor=${c};" vertex="1" parent="1"><mxGeometry x="40" y="${ly}" width="26" height="14" as="geometry"/></mxCell>`);
    r.raw(`<mxCell id="${r.id()}" value="${xmlEsc(t)}" style="text;html=1;fontSize=11;verticalAlign=middle;" vertex="1" parent="1"><mxGeometry x="74" y="${ly - 3}" width="400" height="20" as="geometry"/></mxCell>`);
    ly += 21;
  }
  r.note(520, yB, 1000, 235,
    '<b>Convenciones del modelo físico</b><br/>' +
    '• PK uuid generadas por la aplicación; fechas en <b>timestamptz</b> (UTC). Enums persistidos como texto (varchar) para legibilidad y auditoría.<br/>' +
    '• <b>xmin</b> = token de concurrencia optimista de PostgreSQL mapeado por EF Core/Npgsql (ADR-0007).<br/>' +
    '• Unicidad e integridad en BD: índices únicos parciales y <b>EXCLUDE USING gist</b> (requiere la extensión btree_gist) impiden la doble asignación de un conductor o vehículo en franjas solapadas.<br/>' +
    '• Silo: cada operador tiene su propia BD con este esquema completo. <b>empresa_cliente_id</b> identifica al cliente empresarial atendido (aerolínea, minera…), nunca al tenant.<br/>' +
    '• Herencia de usuario como tablas por tipo (TPT): ops.pasajero y ops.conductor comparten PK con ops.usuario.<br/>' +
    '• Las FK de actor (actor_id, cancelada_por_id, reportado_por_id, creada_por_id, modificado_por_id, destinatario_id), conversacion_whatsapp.usuario_id y las del decision log hacia conductor/vehículo apuntan a ops.usuario / ops.vehiculo. No se dibujan para mantener la legibilidad.<br/>' +
    '• calificacion.pasajero_id / conductor_id y registro_ubicacion.conductor_id se guardan desnormalizados para reportes y consultas por conductor (en el diagrama de clases se obtienen navegando Viaje → Asignacion).<br/>' +
    '• motor.decision_asignacion, evaluacion_candidato y resultado_regla persisten AssignmentDecisionLog, CandidateEvaluation y RuleOutcome (página 2).<br/>' +
    '• Sin credenciales: la identidad la gestiona el proveedor OIDC (ADR-0003); ops.usuario solo guarda identidad_externa_id.',
  );
  r.note(1550, yB, 700, 235,
    '<b>Alcance de la implementación actual (RF-10)</b><br/>' +
    'Se implementa: rol, usuario, pasajero, conductor, empresa_cliente, itinerario, turno, vehiculo, solicitud_viaje, estado_solicitud_historial, asignacion, esquema motor completo.<br/><br/>' +
    'Modelado pero <b>pendiente hasta que se pida</b>: notif.* (Outbox, WhatsApp), telemetria.*, audit.*, plataforma.* (Keycloak / Key Vault), calificación, viajes, paradas e incidentes.',
    { fill: '#FFF3E0', stroke: '#D79B00' });
}

const file = `<mxfile host="FlowRide-gen" modified="${new Date().toISOString()}" agent="FlowRide generator" version="24.0.0" type="device">${d.xml(1600, 1200)}${m.xml(1600, 1200)}${r.xml(1600, 1200)}</mxfile>`;
fs.writeFileSync(OUT, file, 'utf8');
console.log('OK', OUT, file.length);
