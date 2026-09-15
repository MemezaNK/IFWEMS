import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CaseService } from '../../core/cases/case.service';
import { CaseDto } from '../../core/models/case.models';

@Component({
  selector: 'app-case-list',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './case-list.component.html',
  styleUrl: './case-list.component.scss'
})
export class CaseListComponent implements OnInit {
  readonly cases = signal<CaseDto[]>([]);
  readonly isLoading = signal(true);

  constructor(private readonly caseService: CaseService) {}

  ngOnInit(): void {
    this.caseService.getAll().subscribe({
      next: (cases) => {
        this.cases.set(cases);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }
}
