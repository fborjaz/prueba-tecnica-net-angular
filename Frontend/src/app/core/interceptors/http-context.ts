import { HttpContextToken } from '@angular/common/http';

/** Marca una petición cuyo error lo muestra el propio formulario (no se lanza el toast global). */
export const SKIP_ERROR_TOAST = new HttpContextToken<boolean>(() => false);

/** Excluye la petición de la barra de carga global (p. ej. refrescos silenciosos). */
export const SKIP_LOADING = new HttpContextToken<boolean>(() => false);
