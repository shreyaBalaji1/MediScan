import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface DrugResult {
  name: string;
  purpose?: string;
  warnings: string;
}

export interface DrugInteractionSide {
  name: string;
  drugInteractions: string;
}

export interface InteractionResult {
  drugA: DrugInteractionSide;
  drugB: DrugInteractionSide;
  possibleInteractionFlagged: boolean;
  disclaimer: string;
}

@Injectable({
  providedIn: 'root'
})
export class DrugService {
  private apiUrl = `${environment.apiBaseUrl}/drugs`;

  constructor(private http: HttpClient) {}

  searchDrug(name: string): Observable<DrugResult> {
    return this.http.get<DrugResult>(
      `${this.apiUrl}/search?name=${encodeURIComponent(name)}`
    );
  }

  checkInteraction(drugA: string, drugB: string): Observable<InteractionResult> {
    return this.http.get<InteractionResult>(
      `${this.apiUrl}/interactions?drugA=${encodeURIComponent(drugA)}&drugB=${encodeURIComponent(drugB)}`
    );
  }
}