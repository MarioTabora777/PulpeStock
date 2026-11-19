const formulario = document.querySelector("[data-conteo]");
const unidades = formulario.querySelector("[data-unidades]");
const lista = formulario.querySelector("[data-paquetes]");
const agregar = formulario.querySelector("[data-agregar]");
const total = formulario.querySelector("[data-total]");
const resultado = total.querySelector("output");
const plantilla = document.querySelector("#fila-paquete");
const maximo = 2147483647;

function actualizarTotal() {
    const paquetes = Array.from(lista.querySelectorAll("[data-paquete]"))
        .map(fila => Array.from(fila.querySelectorAll("input")))
        .filter(campos => campos.some(campo => campo.value !== "" || campo.validity.badInput));

    const camposIngresados = paquetes.flat();
    if (unidades.value !== "" || unidades.validity.badInput) {
        camposIngresados.push(unidades);
    }

    if (camposIngresados.length === 0 ||
        camposIngresados.some(campo => !campo.validity.valid || !/^\d+$/.test(campo.value))) {
        resultado.value = "—";
        return;
    }

    let cantidad = Number(unidades.value);
    for (const [paquetesContados, unidadesPorPaquete] of paquetes) {
        cantidad += Number(paquetesContados.value) * Number(unidadesPorPaquete.value);
        if (cantidad > maximo) {
            resultado.value = "Cantidad demasiado alta";
            return;
        }
    }

    resultado.value = cantidad.toLocaleString("es-HN") + " unidades";
}

function numerarFilas() {
    const filas = lista.querySelectorAll("[data-paquete]");

    filas.forEach((fila, indice) => {
        fila.querySelectorAll("[data-campo]").forEach(campo => {
            const propiedad = campo.dataset.campo;
            campo.name = "Paquetes[" + indice + "]." + propiedad;
            campo.id = "Paquetes_" + indice + "__" + propiedad;
            fila.querySelector('[data-label="' + propiedad + '"]').htmlFor = campo.id;
        });
        fila.querySelector("[data-quitar]").hidden = filas.length === 1;
    });
}

agregar.addEventListener("click", () => {
    lista.append(plantilla.content.cloneNode(true));
    numerarFilas();
    lista.lastElementChild.querySelector("input").focus();
});

lista.addEventListener("click", evento => {
    const boton = evento.target.closest("[data-quitar]");
    if (boton) {
        boton.closest("[data-paquete]").remove();
        numerarFilas();
        actualizarTotal();
        agregar.focus();
    }
});

formulario.addEventListener("input", actualizarTotal);
agregar.hidden = false;
total.hidden = false;
numerarFilas();
actualizarTotal();
