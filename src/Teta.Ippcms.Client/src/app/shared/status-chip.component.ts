import { Component, Input } from '@angular/core';

/** Colour-coded status label; colour is never the only signal (text is always shown). */
@Component({
  selector: 'teta-status',
  standalone: true,
  template: `@if (value !== null && value !== undefined && value !== '') { <span class="chip" [class]="'chip ' + css">{{ label }}</span> }`
})
export class StatusChipComponent {
  @Input() value: unknown;

  get label(): string {
    return String(this.value).replace(/([a-z])([A-Z])/g, '$1 $2');
  }

  get css(): string {
    return String(this.value).toLowerCase().replace(/[^a-z]/g, '');
  }
}
