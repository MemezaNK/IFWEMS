import { AsyncPipe } from '@angular/common';
import { Component, Inject, OnInit, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Observable, firstValueFrom, of } from 'rxjs';
import { Option } from '../core/reference.service';

export type FieldType = 'text' | 'textarea' | 'number' | 'date' | 'datetime' | 'select' | 'multiselect' | 'checkbox' | 'password' | 'email';

export interface Field {
  key: string;
  label: string;
  type?: FieldType;
  required?: boolean;
  min?: number;
  max?: number;
  maxLength?: number;
  hint?: string;
  options?: Option[] | Observable<Option[]>;
  /** Full-width field (textareas default to full width). */
  wide?: boolean;
}

export interface FormDialogData {
  title: string;
  fields: Field[];
  value?: Record<string, unknown>;
  submitLabel?: string;
  intro?: string;
}

/**
 * Generic create/edit dialog built from a field list. Mandatory fields are marked and validated
 * client-side; the server validates again (SRS §7.1). Closing with unsaved changes asks first.
 */
@Component({
  selector: 'teta-form-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatCheckboxModule, ReactiveFormsModule, AsyncPipe],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content>
        @if (data.intro) { <p class="muted">{{ data.intro }}</p> }
        <div class="form-grid">
          @for (f of data.fields; track f.key) {
            @switch (f.type ?? 'text') {
              @case ('checkbox') {
                <mat-checkbox [formControlName]="f.key" style="grid-column: 1 / -1; margin: 4px 0 12px">{{ f.label }}</mat-checkbox>
              }
              @case ('textarea') {
                <mat-form-field appearance="outline" style="grid-column: 1 / -1">
                  <mat-label>{{ f.label }}</mat-label>
                  <textarea matInput rows="3" [formControlName]="f.key" [required]="!!f.required"></textarea>
                  @if (f.hint) { <mat-hint>{{ f.hint }}</mat-hint> }
                  @if (form.controls[f.key].invalid) { <mat-error>{{ errorText(f) }}</mat-error> }
                </mat-form-field>
              }
              @case ('select') {
                <mat-form-field appearance="outline" [style.grid-column]="f.wide ? '1 / -1' : null">
                  <mat-label>{{ f.label }}</mat-label>
                  <mat-select [formControlName]="f.key" [required]="!!f.required">
                    @if (!f.required) { <mat-option [value]="null">—</mat-option> }
                    @for (o of options[f.key] | async; track o.value) { <mat-option [value]="o.value">{{ o.label }}</mat-option> }
                  </mat-select>
                  @if (f.hint) { <mat-hint>{{ f.hint }}</mat-hint> }
                  @if (form.controls[f.key].invalid) { <mat-error>{{ errorText(f) }}</mat-error> }
                </mat-form-field>
              }
              @case ('multiselect') {
                <mat-form-field appearance="outline" style="grid-column: 1 / -1">
                  <mat-label>{{ f.label }}</mat-label>
                  <mat-select multiple [formControlName]="f.key">
                    @for (o of options[f.key] | async; track o.value) { <mat-option [value]="o.value">{{ o.label }}</mat-option> }
                  </mat-select>
                </mat-form-field>
              }
              @default {
                <mat-form-field appearance="outline" [style.grid-column]="f.wide ? '1 / -1' : null">
                  <mat-label>{{ f.label }}</mat-label>
                  <input matInput [type]="inputType(f)" [formControlName]="f.key" [required]="!!f.required" />
                  @if (f.hint) { <mat-hint>{{ f.hint }}</mat-hint> }
                  @if (form.controls[f.key].invalid) { <mat-error>{{ errorText(f) }}</mat-error> }
                </mat-form-field>
              }
            }
          }
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" (click)="cancel()">Cancel</button>
        <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid">{{ data.submitLabel ?? 'Save' }}</button>
      </mat-dialog-actions>
    </form>
  `
})
export class FormDialogComponent implements OnInit {
  form = new FormGroup<Record<string, FormControl>>({});
  options: Record<string, Observable<Option[]>> = {};

  constructor(@Inject(MAT_DIALOG_DATA) public data: FormDialogData, private ref: MatDialogRef<FormDialogComponent>) {
    ref.disableClose = true;
  }

  ngOnInit(): void {
    for (const f of this.data.fields) {
      const validators: ValidatorFn[] = [];
      if (f.required) validators.push(f.type === 'checkbox' ? Validators.requiredTrue : Validators.required);
      if (f.min !== undefined) validators.push(Validators.min(f.min));
      if (f.max !== undefined) validators.push(Validators.max(f.max));
      if (f.maxLength) validators.push(Validators.maxLength(f.maxLength));
      if (f.type === 'email') validators.push(Validators.email);
      let initial = this.data.value?.[f.key] ?? (f.type === 'checkbox' ? false : f.type === 'multiselect' ? [] : null);
      if (f.type === 'date' && typeof initial === 'string') initial = initial.substring(0, 10);
      if (f.type === 'datetime' && typeof initial === 'string') initial = initial.substring(0, 16);
      this.form.addControl(f.key, new FormControl(initial, validators));
      if (f.type === 'select' || f.type === 'multiselect') {
        this.options[f.key] = Array.isArray(f.options) ? of(f.options) : (f.options ?? of([]));
      }
    }
  }

  inputType(f: Field): string {
    switch (f.type) {
      case 'number': return 'number';
      case 'date': return 'date';
      case 'datetime': return 'datetime-local';
      case 'password': return 'password';
      case 'email': return 'email';
      default: return 'text';
    }
  }

  errorText(f: Field): string {
    const c = this.form.controls[f.key];
    if (c.hasError('required')) return 'This field is required.';
    if (c.hasError('min')) return `Must be at least ${f.min}.`;
    if (c.hasError('max')) return `Must be at most ${f.max}.`;
    if (c.hasError('maxlength')) return `Maximum ${f.maxLength} characters.`;
    if (c.hasError('email')) return 'Enter a valid e-mail address.';
    return 'Invalid value.';
  }

  submit(): void {
    if (this.form.invalid) return;
    const value: Record<string, unknown> = {};
    for (const f of this.data.fields) {
      let v = this.form.controls[f.key].value;
      if (f.type === 'number' && v !== null && v !== '') v = Number(v);
      if (v === '') v = null;
      if (f.type === 'datetime' && v) v = new Date(v as string).toISOString();
      value[f.key] = v;
    }
    this.ref.close(value);
  }

  cancel(): void {
    if (this.form.dirty && !confirm('Discard unsaved changes?')) return;
    this.ref.close();
  }
}

/** Opens the generic form and resolves with the entered values (undefined when cancelled). */
export async function openForm<T = any>(dialog: MatDialog, data: FormDialogData, width = '720px'): Promise<T | undefined> {
  const ref = dialog.open(FormDialogComponent, { data, width, maxWidth: '96vw', autoFocus: 'first-tabbable' });
  return firstValueFrom(ref.afterClosed()) as Promise<T | undefined>;
}

/** Asks for confirmation, optionally with a mandatory reason. Resolves with the reason ('' when not needed) or undefined. */
export async function confirmAction(dialog: MatDialog, title: string, message: string, reasonLabel?: string): Promise<string | undefined> {
  const fields: Field[] = reasonLabel ? [{ key: 'reason', label: reasonLabel, type: 'textarea', required: true, maxLength: 1000 }] : [];
  const result = await openForm<{ reason?: string }>(dialog, { title, intro: message, fields, submitLabel: 'Confirm' }, '520px');
  return result === undefined ? undefined : (result.reason ?? '');
}

export function useDialog(): MatDialog {
  return inject(MatDialog);
}
