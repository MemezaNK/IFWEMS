import { JsonPipe } from '@angular/common';
import { Component, Inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';

/** Read-only pretty-printed JSON viewer, used for board-pack datasets and similar snapshots. */
@Component({
  selector: 'teta-json-viewer',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, JsonPipe],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      <pre class="small" style="white-space: pre-wrap; word-break: break-word">{{ data.json | json }}</pre>
    </mat-dialog-content>
    <mat-dialog-actions align="end"><button mat-button mat-dialog-close>Close</button></mat-dialog-actions>
  `
})
export class JsonViewerDialog {
  constructor(@Inject(MAT_DIALOG_DATA) public data: { title: string; json: unknown }) {}
}
