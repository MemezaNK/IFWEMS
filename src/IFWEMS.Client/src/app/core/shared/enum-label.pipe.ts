import { Pipe, PipeTransform } from '@angular/core';

/**
 * Renders a PascalCase enum value (e.g. "UnderInvestigation", from the API's
 * JsonStringEnumConverter) as a readable label ("Under Investigation") for
 * display in tables, chips and dropdowns across the app.
 */
@Pipe({
  name: 'enumLabel',
  standalone: true
})
export class EnumLabelPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value) {
      return '';
    }
    return value
      .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
      .replace(/([A-Z]+)([A-Z][a-z])/g, '$1 $2')
      .trim();
  }
}
