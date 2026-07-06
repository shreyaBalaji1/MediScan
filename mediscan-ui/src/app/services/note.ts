import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../environments/environment';

export interface DrugNote {
  id?: number;
  drugName: string;
  noteText: string;
  createdAt?: string | Date;
}

@Injectable({
  providedIn: 'root'
})
export class NoteService {
  private apiUrl = `${environment.apiBaseUrl}/notes`;

  constructor(private http: HttpClient) {}

  getNotes(drugName?: string): Observable<DrugNote[]> {
    if (drugName) {
      return this.http.get<any[]>(
        `${this.apiUrl}?drugName=${encodeURIComponent(drugName)}`
      ).pipe(
        map(items => items.map(n => ({
          id: n.id ?? n.Id,
          drugName: n.drugName ?? n.DrugName,
          noteText: n.noteText ?? n.NoteText,
          createdAt: new Date(n.createdAt ?? n.CreatedAt)
        } as DrugNote)))
      );
    }

    return this.http.get<any[]>(this.apiUrl).pipe(
      map(items => items.map(n => ({
        id: n.id ?? n.Id,
        drugName: n.drugName ?? n.DrugName,
        noteText: n.noteText ?? n.NoteText,
        createdAt: new Date(n.createdAt ?? n.CreatedAt)
      } as DrugNote)))
    );
  }

  addNote(note: DrugNote): Observable<DrugNote> {
    return this.http.post<any>(this.apiUrl, note).pipe(
      map(n => ({
        id: n.id ?? n.Id,
        drugName: n.drugName ?? n.DrugName,
        noteText: n.noteText ?? n.NoteText,
        createdAt: new Date(n.createdAt ?? n.CreatedAt)
      } as DrugNote))
    );
  }

  updateNote(note: DrugNote): Observable<DrugNote> {
    if (!note.id) throw new Error('Note id is required');
    // Send a minimal payload (only the updated text) to avoid any casing/format issues
    const body = { noteText: note.noteText };
    return this.http.put<any>(`${this.apiUrl}/${note.id}`, body).pipe(
      map(n => ({
        id: n.id ?? n.Id,
        drugName: n.drugName ?? n.DrugName,
        noteText: n.noteText ?? n.NoteText,
        createdAt: new Date(n.createdAt ?? n.CreatedAt)
      } as DrugNote))
    );
  }

  deleteNote(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}