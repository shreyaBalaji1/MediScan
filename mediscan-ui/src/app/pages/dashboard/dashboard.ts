import { Component, ChangeDetectorRef, OnInit, NgZone } from '@angular/core';
import { finalize } from 'rxjs/operators';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { DrugService, DrugResult, InteractionResult } from '../../services/drug';
import { FavoriteService, FavoriteDrug } from '../../services/favorite';
import { NoteService, DrugNote } from '../../services/note';
import { AuthService } from '../../services/auth';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css'
})
export class Dashboard implements OnInit {
  drugName = '';
  drugResult: DrugResult | null = null;
  errorMessage = '';

  isLoading = false;

  favorites: FavoriteDrug[] = [];
  favoriteMessage = '';
  isFavorite = false;
  // Notes for the currently displayed drug
  notes: DrugNote[] = [];
  newNoteText = '';
  // Message for note-related errors (edit/save failures)
  noteMessage: string | null = null;
  // Map from lowercased drugName -> notes for favorites list
  favoriteNotes: { [drugName: string]: DrugNote[] } = {};
  // Per-note edit buffers: noteId -> draft text
  editingNoteText: { [id: number]: string } = {};
  // Id of note currently being saved/updated
  savingNoteId: number | null = null;
  // Per-favorite new note input buffer
  favoriteNewNote: { [key: string]: string } = {};
  // Controls whether the "new note" input is visible for a favorite
  favoriteShowNewNote: { [key: string]: boolean } = {};

  // Interaction checker
  interactionDrugA = '';
  interactionDrugB = '';
  interactionResult: InteractionResult | null = null;
  interactionError = '';
  isCheckingInteraction = false;
  showFullInteractionText: { a: boolean; b: boolean } = { a: false, b: false };

  constructor(
    private drugService: DrugService,
    private favoriteService: FavoriteService,
    private noteService: NoteService,
    private auth: AuthService,
    private router: Router,
    private cdr: ChangeDetectorRef,
    private ngZone: NgZone
  ) {}

  ngOnInit(): void {
    this.loadFavorites();
  }

  logout() {
    this.auth.logout();
    this.router.navigate(['/login']);
  }

  checkInteraction() {
    this.interactionError = '';
    this.interactionResult = null;
    this.showFullInteractionText = { a: false, b: false };

    const drugA = this.interactionDrugA.trim();
    const drugB = this.interactionDrugB.trim();

    if (!drugA || !drugB) {
      this.interactionError = 'Enter both drug names.';
      return;
    }

    this.isCheckingInteraction = true;
    this.drugService.checkInteraction(drugA, drugB).subscribe({
      next: (result) => {
        this.ngZone.run(() => {
          this.interactionResult = result;
          this.isCheckingInteraction = false;
          this.cdr.detectChanges();
        });
      },
      error: (err) => {
        this.ngZone.run(() => {
          this.interactionError = err?.status === 404
            ? 'One of those drugs could not be found.'
            : 'Could not check interaction. Please try again.';
          this.isCheckingInteraction = false;
          this.cdr.detectChanges();
        });
      }
    });
  }

  searchDrug() {
    this.errorMessage = '';
    this.drugResult = null;
    this.favoriteMessage = '';

    const trimmedName = this.drugName.trim();

    if (!trimmedName) {
      this.errorMessage = 'Please enter a drug name.';
      this.cdr.detectChanges();
      return;
    }

    this.isLoading = true;
    this.cdr.detectChanges();

    this.drugService.searchDrug(trimmedName).subscribe({
      next: (result) => {
        this.ngZone.run(() => {
          this.drugResult = result;
          // Update favorite flag based on currently loaded favorites
          this.checkIfCurrentFavorite();
          // Load notes for this drug
          this.loadNotes(this.drugResult.name);
          this.isLoading = false;
          this.cdr.detectChanges();
        });
      },
      error: () => {
        this.ngZone.run(() => {
          this.errorMessage = 'Drug not found or server error.';
          this.isLoading = false;
          this.cdr.detectChanges();
        });
      }
    });
  }

  loadFavorites() {
    this.favoriteService.getFavorites().subscribe({
      next: (data) => {
        this.ngZone.run(() => {
          this.favorites = data;
          // Re-evaluate whether the currently displayed drug is already favorited
          this.checkIfCurrentFavorite();
          // Load notes for each favorite so they can be shown in the favorites list
          this.loadNotesForFavorites();
          this.cdr.detectChanges();
        });
      },
      error: () => {
        this.ngZone.run(() => {
          this.favoriteMessage = 'Could not load favorites.';
          this.cdr.detectChanges();
        });
      }
    });
  }

  // Load notes for each favorite and cache them in `favoriteNotes`
  loadNotesForFavorites() {
    this.favoriteNotes = {};
    for (const fav of this.favorites) {
      const name = (fav.drugName || '').trim();
      if (!name) continue;
      this.loadNotesForFavorite(name);
    }
  }

  // Compute a safe key for favorite lookup (used by template and notes cache)
  getFavoriteKey(fav: FavoriteDrug) {
    return (fav?.drugName ?? '').trim().toLowerCase();
  }

  // Load notes for a single favorite and cache them for the favorites list
  loadNotesForFavorite(drugName?: string) {
    if (!drugName) return;
    const key = (drugName || '').trim().toLowerCase();
    this.noteService.getNotes(drugName).subscribe({
      next: (data) => {
        this.ngZone.run(() => {
          this.favoriteNotes[key] = data || [];
          this.cdr.detectChanges();
        });
      },
      error: () => {
        this.ngZone.run(() => {
          this.favoriteNotes[key] = [];
          this.cdr.detectChanges();
        });
      }
    });
  }

  // Helper: sets `isFavorite` based on current `drugResult` and loaded `favorites`
  checkIfCurrentFavorite() {
    if (!this.drugResult) {
      this.isFavorite = false;
      return;
    }

    const currentName = (this.drugResult.name || '').trim().toLowerCase();
    this.isFavorite = this.favorites.some(f =>
      (f.drugName || '').trim().toLowerCase() === currentName
    );
  }

  // Notes: load notes for a given drug name (or all if empty)
  loadNotes(drugName?: string) {
    this.noteService.getNotes(drugName).subscribe({
      next: (data) => {
        this.ngZone.run(() => {
          this.notes = data || [];
          this.cdr.detectChanges();
        });
      },
      error: () => {
        this.ngZone.run(() => {
          // keep existing notes if load fails
          this.cdr.detectChanges();
        });
      }
    });
  }

  addNote() {
    if (!this.drugResult) return;
    const text = (this.newNoteText || '').trim();
    if (!text) return;

    const note: DrugNote = {
      drugName: this.drugResult.name,
      noteText: text
    };

    this.noteService.addNote(note).subscribe({
      next: () => {
        this.ngZone.run(() => {
          this.newNoteText = '';
          this.loadNotes(this.drugResult!.name);
          this.cdr.detectChanges();
        });
      },
      error: () => {
        this.ngZone.run(() => {
          // optionally set a message, for now just trigger change detection
          this.cdr.detectChanges();
        });
      }
    });
  }

  // Start editing a note: initialize buffer
  startEdit(note: DrugNote) {
    if (!note || !note.id) return;
    this.editingNoteText[note.id] = note.noteText;
    this.cdr.detectChanges();
  }

  cancelEdit(note: DrugNote) {
    if (!note || !note.id) return;
    delete this.editingNoteText[note.id];
    this.cdr.detectChanges();
  }

  saveEdit(note: DrugNote) {
    if (!note || !note.id) return;
    const draft = (this.editingNoteText[note.id] || '').trim();
    if (!draft) return;
    const updated: DrugNote = { ...note, noteText: draft };
    this.noteMessage = null;
    this.savingNoteId = note.id!;
    try {
      // Ensure savingNoteId is cleared no matter what (success/error), using finalize
      this.noteService.updateNote(updated).pipe(
      finalize(() => {
        // run inside NgZone to ensure change detection picks up the change
        this.ngZone.run(() => {
          this.savingNoteId = null;
          this.cdr.detectChanges();
        });
      })
      ).subscribe({
      next: () => {
        this.ngZone.run(() => {
          delete this.editingNoteText[note.id!];
          // refresh notes or favorites after a successful update
          if (this.drugResult) {
            this.loadNotes(this.drugResult.name);
          } else {
            this.loadFavorites();
          }
          this.cdr.detectChanges();
        });
      },
      error: () => {
        this.ngZone.run(() => {
            this.noteMessage = 'Failed to save note. Please try again.';
            this.cdr.detectChanges();
        });
      }
    });
    } catch (err: any) {
      // Synchronous errors (e.g. missing id thrown by service) — clear saving flag and show message
      this.ngZone.run(() => {
        this.savingNoteId = null;
        this.noteMessage = err?.message || 'Failed to save note.';
        this.cdr.detectChanges();
      });
    }
  }

  // Add a note directly from a favorite card
  addNoteToFavorite(fav: FavoriteDrug) {
    const key = this.getFavoriteKey(fav);
    const text = (this.favoriteNewNote[key] || '').trim();
    if (!text) return;
    const note: DrugNote = { drugName: fav.drugName, noteText: text };
    this.noteService.addNote(note).subscribe({
      next: () => {
        this.ngZone.run(() => {
          this.favoriteNewNote[key] = '';
          // hide the new-note input after successful add
          this.favoriteShowNewNote[key] = false;
          this.loadNotesForFavorite(fav.drugName);
          this.cdr.detectChanges();
        });
      },
      error: () => {
        this.ngZone.run(() => this.cdr.detectChanges());
      }
    });
  }

  deleteNote(id?: number) {
    if (!id) return;
    this.noteService.deleteNote(id).subscribe({
      next: () => {
        this.ngZone.run(() => {
          if (this.drugResult) {
            this.loadNotes(this.drugResult.name);
          } else {
            // if we're deleting a note from the favorites list view, refresh favorites' notes
            this.loadFavorites();
          }
          this.cdr.detectChanges();
        });
      },
      error: () => {
        this.ngZone.run(() => this.cdr.detectChanges());
      }
    });
  }

  saveFavorite() {
    if (!this.drugResult) {
      return;
    }

    // Ensure we have the latest favorites from server to avoid duplicates
    this.favoriteMessage = '';
    this.favoriteService.getFavorites().subscribe({
      next: (currentFavorites) => {
        this.ngZone.run(() => {
          const nameToAdd = this.drugResult!.name.trim().toLowerCase();
          const alreadySaved = currentFavorites.some(f =>
            (f.drugName || '').trim().toLowerCase() === nameToAdd
          );

          if (alreadySaved) {
            this.favoriteMessage = 'This drug is already in favorites.';
            this.cdr.detectChanges();
            return;
          }

          const favorite: FavoriteDrug = {
            drugName: this.drugResult!.name,
            purpose: this.drugResult!.purpose,
            warnings: this.drugResult!.warnings
          };

          this.favoriteService.addFavorite(favorite).subscribe({
            next: () => {
              this.ngZone.run(() => {
                this.favoriteMessage = 'Saved to favorites.';
                this.isFavorite = true;
                // If the user entered a note, save it together with the favorite
                const pendingNote = (this.newNoteText || '').trim();
                if (pendingNote) {
                  const note: DrugNote = { drugName: this.drugResult!.name, noteText: pendingNote };
                  this.noteService.addNote(note).subscribe({
                    next: () => {
                      this.newNoteText = '';
                      this.loadNotes(this.drugResult!.name);
                    },
                    error: () => {
                      // ignore note save failure for now
                    }
                  });
                }

                this.loadFavorites();
                this.cdr.detectChanges();
              });
            },
            error: (error) => {
              this.ngZone.run(() => {
                if (error && error.status === 409) {
                  this.favoriteMessage = 'This drug is already in favorites.';
                } else {
                  this.favoriteMessage = 'Could not save favorite.';
                }
                this.cdr.detectChanges();
              });
            }
          });
        });
      },
      error: () => {
        // If we can't load current favorites, still attempt to add but warn user
        this.ngZone.run(() => {
          const favorite: FavoriteDrug = {
            drugName: this.drugResult!.name,
            purpose: this.drugResult!.purpose,
            warnings: this.drugResult!.warnings
          };

          this.favoriteService.addFavorite(favorite).subscribe({
            next: () => {
              this.ngZone.run(() => {
                this.favoriteMessage = 'Saved to favorites.';
                // if the user has typed a note, save it as well
                const pendingNote = (this.newNoteText || '').trim();
                if (pendingNote) {
                  const note: DrugNote = { drugName: this.drugResult!.name, noteText: pendingNote };
                  this.noteService.addNote(note).subscribe({
                    next: () => {
                      this.newNoteText = '';
                      this.loadNotes(this.drugResult!.name);
                    },
                    error: () => {
                      // ignore note save failure here
                    }
                  });
                }

                this.loadFavorites();
                this.cdr.detectChanges();
              });
            },
            error: () => {
              this.ngZone.run(() => {
                this.favoriteMessage = 'Could not save favorite.';
                this.cdr.detectChanges();
              });
            }
          });
        });
      }
    });
  }

  deleteFavorite(id?: number) {
    if (!id) {
      return;
    }

    this.favoriteService.deleteFavorite(id).subscribe({
      next: () => {
        this.ngZone.run(() => {
          this.favoriteMessage = 'Favorite removed.';
          this.loadFavorites();
          this.cdr.detectChanges();
        });
      },
      error: () => {
        this.ngZone.run(() => {
          this.favoriteMessage = 'Could not delete favorite.';
          this.cdr.detectChanges();
        });
      }
    });
  }
}
