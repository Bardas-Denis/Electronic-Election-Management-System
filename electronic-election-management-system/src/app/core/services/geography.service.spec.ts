import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../../environments/environment';
import { GeographyService } from './geography.service';

describe('GeographyService', () => {
  let service: GeographyService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [GeographyService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(GeographyService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('requests every country', () => {
    service.getCountries().subscribe(result => expect(result).toEqual([]));

    const request = http.expectOne(`${environment.apiUrl}/geography/countries`);
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('requests the children of one node', () => {
    service.getChildren('node-id').subscribe();

    const request = http.expectOne(`${environment.apiUrl}/geography/node-id/children`);
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });
});
