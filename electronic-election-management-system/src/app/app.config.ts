import { ApplicationConfig } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';
import { routes } from './app.routes';
import { jwtInterceptor } from './core/interceptors/jwt.interceptor';
import { authErrorInterceptor } from './core/interceptors/auth-error.interceptor';

// Global config: router + HTTP client with JWT interceptor + ngx-translate
// Order matters: jwtInterceptor attaches the token on the way out,
// authErrorInterceptor watches for 401s on the way back.
export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptors([jwtInterceptor, authErrorInterceptor])),
    provideTranslateService({
      lang: 'ro',
      fallbackLang: 'ro',
      loader: provideTranslateHttpLoader({ prefix: '/i18n/', suffix: '.json' }),
    }),
  ]
};