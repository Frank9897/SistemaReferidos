// editar-producto.js — wwwroot/js/editar-producto.js

function confirmarEliminarPdf(pdfId, productoId, nombre) {
    if (!confirm('¿Eliminar el documento «' + nombre + '»?')) return;
    document.getElementById('eliminar-pdfId').value      = pdfId;
    document.getElementById('eliminar-productoId').value = productoId;
    document.getElementById('form-eliminar-pdf').submit();
}

function agregarFila() {
    var cont = document.getElementById('filas-pdf');
    var fila = document.createElement('div');
    fila.style.cssText = 'display:grid; grid-template-columns:1fr 1fr auto; gap:10px; align-items:end; background:var(--bg3); border:1px solid var(--border); border-radius:var(--radius-sm); padding:12px; margin-bottom:8px;';

    var divNombre = document.createElement('div');
    divNombre.className = 'form-group';
    divNombre.style.marginBottom = '0';

    var labelNombre = document.createElement('label');
    labelNombre.style.cssText = 'font-size:12px; margin-bottom:4px; display:block;';
    labelNombre.textContent = 'Nombre del documento';

    var inputNombre = document.createElement('input');
    inputNombre.type = 'text';
    inputNombre.name = 'nombresPdf';
    inputNombre.placeholder = 'Ej: Parte 3 - Limpia vidrios';
    inputNombre.style.width = '100%';

    divNombre.appendChild(labelNombre);
    divNombre.appendChild(inputNombre);

    var divArchivo = document.createElement('div');
    divArchivo.className = 'form-group';
    divArchivo.style.marginBottom = '0';

    var labelArchivo = document.createElement('label');
    labelArchivo.style.cssText = 'font-size:12px; margin-bottom:4px; display:block;';
    labelArchivo.textContent = 'Archivo PDF';

    var inputArchivo = document.createElement('input');
    inputArchivo.type = 'file';
    inputArchivo.name = 'archivosPdf';
    inputArchivo.accept = '.pdf';
    inputArchivo.style.cssText = 'background:var(--bg2); border:1px solid var(--border); border-radius:var(--radius-sm); padding:7px 10px; width:100%; color:var(--text); font-size:12px;';

    divArchivo.appendChild(labelArchivo);
    divArchivo.appendChild(inputArchivo);

    var btnEliminar = document.createElement('button');
    btnEliminar.type = 'button';
    btnEliminar.className = 'btn btn-danger btn-sm';
    btnEliminar.style.height = '38px';
    btnEliminar.textContent = 'X';
    btnEliminar.onclick = function() { fila.remove(); };

    fila.appendChild(divNombre);
    fila.appendChild(divArchivo);
    fila.appendChild(btnEliminar);
    cont.appendChild(fila);
}

function fmt(n) {
    return '$' + n.toLocaleString('es-AR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function actualizarCalculadora() {
    var inputPrecio = document.getElementById('inputPrecio');
    var inputPorcentaje = document.getElementById('inputPorcentaje');
    if (!inputPrecio || !inputPorcentaje) return;

    var precio = parseFloat(inputPrecio.value) || 0;
    var pct    = parseFloat(inputPorcentaje.value) || 0;

    var MP_COMISION = 0.0629; // 6.29% comisión MercadoPago

    var ingreso      = precio * 3;
    var premio       = precio;
    var bonoBase     = precio * pct / 100;
    var bonoMax      = bonoBase * 1.80;
    var comisionMP   = ingreso * MP_COMISION;
    var margen       = ingreso - premio - bonoMax - comisionMP;
    var margenPct    = ingreso > 0 ? (margen / ingreso * 100) : 0;

    document.getElementById('calc-ingreso').textContent    = fmt(ingreso);
    document.getElementById('calc-premio').textContent     = '-' + fmt(premio);
    document.getElementById('calc-bono').textContent       = '-' + fmt(bonoBase);
    document.getElementById('calc-bono-max').textContent   = '-' + fmt(bonoMax);
    document.getElementById('calc-comision-mp').textContent = '-' + fmt(comisionMP);
    document.getElementById('calc-margen').textContent     = fmt(margen);
    document.getElementById('calc-margen-pct').textContent = margenPct.toFixed(1) + '%';

    var elMargen = document.getElementById('calc-margen');
    elMargen.style.color = margen < 0 ? 'var(--red)' : margen < precio * 0.5 ? 'var(--amber)' : 'var(--green)';

    document.querySelectorAll('.fila-efectivo').forEach(function(td) {
        td.textContent = fmt(bonoBase * parseFloat(td.dataset.mult));
    });

    var thEfectivo = document.getElementById('th-bono-efectivo');
    if (thEfectivo) thEfectivo.textContent = 'Con ' + pct + '% base y precio ' + fmt(precio);

    var badge     = document.getElementById('badge-zona');
    var msgAdv    = document.getElementById('msg-advertencia');
    var msgBlock  = document.getElementById('msg-bloqueado');
    var zonaInfo  = document.getElementById('zona-info');
    var btnSubmit = document.getElementById('btn-submit');

    msgAdv.style.display = msgBlock.style.display = 'none';

    if (pct > 66) {
        badge.className = 'badge badge-danger';
        badge.textContent = 'Bloqueado';
        msgBlock.style.display = 'block';
        zonaInfo.style.background = 'rgba(239,68,68,0.08)';
        zonaInfo.style.borderColor = 'rgba(239,68,68,0.3)';
        zonaInfo.style.color = '#FCA5A5';
        zonaInfo.textContent = 'Limite superado. Maximo: 66%.';
        if (btnSubmit) btnSubmit.disabled = true;
    } else if (pct > 30) {
        badge.className = 'badge badge-warning';
        badge.textContent = 'Zona agresiva';
        msgAdv.style.display = 'block';
        zonaInfo.style.background = 'rgba(245,158,11,0.08)';
        zonaInfo.style.borderColor = 'rgba(245,158,11,0.3)';
        zonaInfo.style.color = '#FCD34D';
        zonaInfo.textContent = 'Zona agresiva. Margen minimo: ' + margenPct.toFixed(0) + '%.';
        if (btnSubmit) btnSubmit.disabled = false;
    } else {
        badge.className = 'badge badge-success';
        badge.textContent = 'Zona segura';
        zonaInfo.style.background = 'rgba(34,197,94,0.08)';
        zonaInfo.style.borderColor = 'rgba(34,197,94,0.2)';
        zonaInfo.style.color = '#4ADE80';
        zonaInfo.textContent = 'El negocio retiene el ' + margenPct.toFixed(0) + '% del ingreso como margen minimo (incluye comisión MP 6.29%).';
        if (btnSubmit) btnSubmit.disabled = false;
    }
}

document.addEventListener('DOMContentLoaded', actualizarCalculadora);

