import { DatePipe } from '@angular/common';
import { Component, Input, OnChanges, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ApiService } from '../../core/api.service';

/** Collaboration comments on a record (activity tab). */
@Component({
  selector: 'teta-comments',
  standalone: true,
  imports: [FormsModule, MatFormFieldModule, MatInputModule, MatButtonModule, DatePipe],
  template: `
    <div class="card">
      <h2>Comments</h2>
      <mat-form-field class="full-width"><mat-label>Add a comment</mat-label><textarea matInput rows="2" [(ngModel)]="text"></textarea></mat-form-field>
      <button mat-flat-button color="primary" [disabled]="!text.trim()" (click)="add()">Post</button>
      @for (c of comments(); track c.id) {
        <div style="border-top: 1px solid #eef1f4; margin-top: 10px; padding-top: 8px">
          <div class="small muted">{{ c.authorName }} · {{ c.createdAtUtc | date: 'yyyy-MM-dd HH:mm' }}</div>
          <div style="white-space: pre-wrap">{{ c.text }}</div>
        </div>
      } @empty { <p class="muted">No comments yet.</p> }
    </div>
  `
})
export class CommentsComponent implements OnChanges {
  @Input({ required: true }) parentType!: string;
  @Input({ required: true }) parentId!: string;
  private readonly api = inject(ApiService);
  readonly comments = signal<any[]>([]);
  text = '';

  ngOnChanges(): void {
    this.api.get<any[]>('comments', { parentType: this.parentType, parentId: this.parentId }).subscribe(c => this.comments.set(c));
  }

  add(): void {
    this.api.post('comments', { parentType: this.parentType, parentId: this.parentId, text: this.text.trim() }).subscribe(() => {
      this.text = '';
      this.ngOnChanges();
    });
  }
}
