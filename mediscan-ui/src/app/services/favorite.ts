import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../environments/environment';

export interface FavoriteDrug {
  id?: number;
  drugName: string;
  purpose?: string;
  warnings: string;
  createdAt?: string;
}

@Injectable({
  providedIn: 'root'
})
export class FavoriteService {
  private apiUrl = `${environment.apiBaseUrl}/favorites`;

  constructor(private http: HttpClient) {}

  getFavorites(): Observable<FavoriteDrug[]> {
    return this.http.get<any[]>(this.apiUrl).pipe(
      map(items => items.map(i => ({
        id: i.id ?? i.Id,
        drugName: i.drugName ?? i.DrugName,
        purpose: i.purpose ?? i.Purpose,
        warnings: i.warnings ?? i.Warnings,
        createdAt: i.createdAt ?? i.CreatedAt
      } as FavoriteDrug)))
    );
  }

  addFavorite(favorite: FavoriteDrug): Observable<FavoriteDrug> {
    return this.http.post<any>(this.apiUrl, favorite).pipe(
      map(i => ({
        id: i.id ?? i.Id,
        drugName: i.drugName ?? i.DrugName,
        purpose: i.purpose ?? i.Purpose,
        warnings: i.warnings ?? i.Warnings,
        createdAt: i.createdAt ?? i.CreatedAt
      } as FavoriteDrug))
    );
  }

  deleteFavorite(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}