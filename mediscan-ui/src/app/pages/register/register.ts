import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './register.html',
  styleUrl: '../auth-pages.css'
})
export class Register {
  name = '';
  email = '';
  password = '';
  errorMessage = '';
  isLoading = false;

  constructor(private auth: AuthService, private router: Router) {}

  submit() {
    this.errorMessage = '';

    if (!this.email.trim() || !this.password) {
      this.errorMessage = 'Email and password are required.';
      return;
    }

    if (this.password.length < 6) {
      this.errorMessage = 'Password must be at least 6 characters.';
      return;
    }

    this.isLoading = true;
    this.auth
      .register({ email: this.email.trim(), password: this.password, name: this.name.trim() || undefined })
      .subscribe({
        next: () => {
          this.auth.login({ email: this.email.trim(), password: this.password }).subscribe({
            next: () => {
              this.isLoading = false;
              this.router.navigate(['/']);
            },
            error: () => {
              this.isLoading = false;
              this.router.navigate(['/login']);
            }
          });
        },
        error: (err) => {
          this.isLoading = false;
          this.errorMessage = err?.status === 409
            ? 'An account with that email already exists.'
            : 'Could not create account.';
        }
      });
  }
}
