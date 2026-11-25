import { AbstractControl, FormGroup, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';

/**
 * Validadores con las mismas reglas que la API (Backend/.../ValidationRules.cs),
 * para no consumir el servicio con datos inválidos.
 */
const pattern = (regex: RegExp, key: string, message: string): ValidatorFn =>
  (control: AbstractControl): ValidationErrors | null =>
    !control.value || regex.test(String(control.value)) ? null : { [key]: message };

export const AppValidators = {
  username: [
    Validators.required,
    Validators.minLength(8),
    Validators.maxLength(20),
    pattern(/^[A-Za-z0-9]+$/, 'specialChars', 'Sin caracteres especiales ni espacios.'),
    pattern(/^(?=.*[A-Za-z])(?=.*\d).+$/, 'lettersNumber', 'Debe tener letras y al menos un número.')
  ],
  password: [
    Validators.required,
    Validators.minLength(8),
    Validators.maxLength(30),
    pattern(/\d/, 'number', 'Debe tener al menos un número.'),
    pattern(/[A-Z]/, 'upper', 'Debe tener al menos una letra mayúscula.')
  ],
  personName: [
    Validators.required,
    Validators.maxLength(50),
    pattern(/^[A-Za-zÁÉÍÓÚáéíóúÑñÜü ]+$/, 'letters', 'Solo letras y espacios.')
  ],
  identification: [
    Validators.required,
    pattern(/^\d{10,13}$/, 'identification', 'Solo números, entre 10 y 13 dígitos.')
  ],
  phone: [
    Validators.required,
    pattern(/^09\d{8,11}$/, 'phone', 'Solo números, inicia con 09 y tiene al menos 10 dígitos.')
  ],
  email: (max = 100) => [Validators.required, Validators.email, Validators.maxLength(max)],
  address: [Validators.required, Validators.minLength(20), Validators.maxLength(100)]
};

/** Validador de grupo: dos campos deben coincidir. */
export function matchFields(field: string, confirm: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const g = group as FormGroup;
    const a = g.get(field)?.value;
    const b = g.get(confirm)?.value;
    return a && b && a !== b ? { mismatch: 'Las contraseñas no coinciden.' } : null;
  };
}

/** Primer mensaje de error legible de un control. */
export function controlError(control: AbstractControl | null): string | null {
  if (!control || !control.errors || !(control.touched || control.dirty)) return null;
  const e = control.errors;

  if (e['required']) return 'Este campo es obligatorio.';
  if (e['minlength']) return `Debe tener al menos ${e['minlength'].requiredLength} caracteres.`;
  if (e['maxlength']) return `No debe superar ${e['maxlength'].requiredLength} caracteres.`;
  if (e['email']) return 'El correo no tiene un formato válido.';
  if (e['min']) return `El valor mínimo es ${e['min'].min}.`;
  if (e['server']) return e['server'];

  const custom = Object.values(e).find(v => typeof v === 'string');
  return (custom as string) ?? 'El valor no es válido.';
}

/** Coloca en los controles los errores de validación devueltos por la API. */
export function applyServerErrors(form: FormGroup, errors: Record<string, string[]>): void {
  Object.entries(errors).forEach(([key, messages]) => {
    const control = form.get(key) ?? form.get(key.charAt(0).toLowerCase() + key.slice(1));
    if (control) {
      control.setErrors({ ...(control.errors ?? {}), server: messages[0] });
      control.markAsTouched();
    }
  });
}
