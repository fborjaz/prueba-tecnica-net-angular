import { FormControl, FormGroup } from '@angular/forms';
import { AppValidators, controlError, matchFields } from './validators';

/** Crea un control con las reglas indicadas y devuelve sus errores. */
const errorsOf = (value: string, validators: typeof AppValidators.username) =>
  new FormControl(value, validators).errors;

describe('AppValidators (mismas reglas que la API)', () => {
  it('acepta un usuario válido', () => {
    expect(errorsOf('cajero2027', AppValidators.username)).toBeNull();
  });

  it.each([
    ['abc1', 'minlength'],
    ['usuariodemasiadolargo123', 'maxlength'],
    ['cajero_2027', 'specialChars'],
    ['cajerosinnumero', 'lettersNumber']
  ])('rechaza el usuario "%s" (%s)', (value, error) => {
    expect(errorsOf(value, AppValidators.username)).toHaveProperty(error);
  });

  it.each([
    ['corta1A', 'minlength'],
    ['sinnumeroA', 'number'],
    ['sinmayuscula1', 'upper']
  ])('rechaza la contraseña "%s" (%s)', (value, error) => {
    expect(errorsOf(value, AppValidators.password)).toHaveProperty(error);
  });

  it('la identificación admite solo de 10 a 13 dígitos', () => {
    expect(errorsOf('0912345671', AppValidators.identification)).toBeNull();
    expect(errorsOf('1712345678001', AppValidators.identification)).toBeNull();
    expect(errorsOf('123', AppValidators.identification)).toHaveProperty('identification');
    expect(errorsOf('09123456AB', AppValidators.identification)).toHaveProperty('identification');
  });

  it('el teléfono empieza con 09 y tiene al menos 10 dígitos', () => {
    expect(errorsOf('0987654321', AppValidators.phone)).toBeNull();
    expect(errorsOf('0812345678', AppValidators.phone)).toHaveProperty('phone');
    expect(errorsOf('09123', AppValidators.phone)).toHaveProperty('phone');
  });
});

describe('matchFields', () => {
  it('marca el grupo cuando las contraseñas no coinciden', () => {
    const group = new FormGroup(
      { password: new FormControl('Clave2026'), confirm: new FormControl('Otra2026') },
      { validators: matchFields('password', 'confirm') }
    );
    expect(group.errors).toHaveProperty('mismatch');

    group.controls.confirm.setValue('Clave2026');
    expect(group.errors).toBeNull();
  });
});

describe('controlError', () => {
  it('no muestra errores hasta que el usuario toca el campo', () => {
    const control = new FormControl('', AppValidators.username);
    expect(controlError(control)).toBeNull();

    control.markAsTouched();
    expect(controlError(control)).toBe('Este campo es obligatorio.');
  });
});
