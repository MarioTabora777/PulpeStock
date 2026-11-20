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
