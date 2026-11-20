LEARN-CAP-33FEE1B7


# PulpeStock

PulpeStock registra conteos físicos de inventario para una pulpería familiar. La propietaria puede revisar productos al cierre o cuando tenga tiempo, y consultar cuánto contó y en qué fecha.

## Primer avance

- Registrar productos por nombre, marca y presentación, con código de barras opcional.
- Guardar un conteo directo o sumar unidades sueltas y paquetes cerrados de distintos tamaños.
- Consultar el último conteo y su historial por producto. El conteo de cero indica que el producto estaba agotado cuando se revisó.

Para probar la conversión, registrá una marca de azúcar en bolsa de una libra. Ingresá tres unidades sueltas, un paquete de doce y otro de seis. El total debe ser de 21 unidades. Registrá después un conteo de cero y revisá que ambos queden en el historial.

## Ejecutar en local

Se necesita el SDK de .NET 10. Desde la raíz del repositorio:

```bash
dotnet run --project src/PulpeStock.Web --urls http://localhost:5080
```

Abrí [PulpeStock](http://localhost:5080). La aplicación crea y actualiza la base local `pulpestock.db` mediante migraciones. Los conteos se almacenan en ese archivo.

## Pruebas

```bash
dotnet test PulpeStock.sln
```

Las pruebas unitarias cubren el cálculo de cantidades. Las pruebas de integración envían los formularios y verifican el historial usando SQLite en memoria. GitHub Actions compila y ejecuta las pruebas en cada pull request y en los cambios que llegan a `main`.

## Estructura

- `src/PulpeStock.Web` contiene la aplicación ASP.NET Core MVC, las reglas de conteo y la persistencia con EF Core.
- `tests/PulpeStock.Tests` contiene las pruebas unitarias y de integración.
- `.github/workflows/ci.yml` define la compilación y las pruebas en GitHub Actions.

## Mínimos y reposición

Desde la ficha de un producto, abrí **Configurar mínimo** e ingresá la cantidad de unidades que indica que hace falta reponer. Dejá el campo vacío para desactivar el aviso.

La opción **Reposición** muestra los productos cuyo último conteo es igual o menor a su mínimo. Por ejemplo, un mínimo de tres bolsas incluye conteos de tres, dos, una o cero bolsas. La fecha permite ver cuándo se verificó la cantidad. Los productos con mínimo que todavía requieren su primer conteo aparecen como pendientes de contar.

Al iniciar, la aplicación actualiza las bases existentes con la nueva columna de mínimo. Los conteos anteriores siguen disponibles.

## Consulta de reportes

Desde **Reportes** podés consultar el último conteo de cada producto, con su marca, presentación y fecha. El selector permite consultar un producto específico. Los productos que todavía requieren una primera revisión muestran **Sin conteo**.

En **Historial de conteos** podés combinar el producto con las fechas Desde y Hasta. Las fechas incluyen el día completo en hora de Honduras. Podés dejar una fecha vacía para consultar desde el inicio o hasta el final del historial, o dejar ambas vacías para ver todos los conteos. Los resultados se ordenan del más reciente al más antiguo.

Los filtros se aplican al pulsar **Consultar**. **Limpiar filtros** vuelve a la consulta completa.

## Exportación de reportes

Los reportes de inventario, historial y reposición permiten **Descargar CSV** con los filtros que estén seleccionados en el formulario. El archivo incluye encabezados y se guarda en UTF-8 para conservar las tildes al abrirlo en Excel. Los campos con apariencia de fórmula se exportan como texto.

Desde **Reportes**, abrí **Reporte de reposición** para consultar los productos que llegaron a su mínimo y los que aún están pendientes de contar. Podés filtrar por producto. El CSV incluye la marca, presentación, último conteo, mínimo, fecha y estado. Cuando falta un conteo, la cantidad y la fecha quedan vacías y el estado indica que está pendiente.

Si una consulta no tiene resultados, la descarga contiene únicamente los encabezados. Los filtros inválidos deben corregirse antes de descargar.
