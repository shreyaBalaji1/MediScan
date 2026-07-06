import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

import { DrugService } from './drug';

describe('DrugService', () => {
  let service: DrugService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(DrugService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
