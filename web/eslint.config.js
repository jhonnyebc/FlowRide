import js from '@eslint/js';
import reactHooks from 'eslint-plugin-react-hooks';
import globals from 'globals';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  { ignores: ['dist', 'src/api/esquema.d.ts'] },
  {
    files: ['**/*.{ts,tsx}'],
    extends: [js.configs.recommended, ...tseslint.configs.recommended],
    languageOptions: { ecmaVersion: 2022, globals: globals.browser },
    plugins: { 'react-hooks': reactHooks },
    rules: {
      ...reactHooks.configs.recommended.rules,
      // Sin secretos en el código del navegador: lo que va en import.meta.env termina en el JavaScript público.
      'no-restricted-syntax': [
        'error',
        {
          selector: "MemberExpression[property.name=/SECRET|TOKEN|PASSWORD|CLAVE/i][object.property.name='env']",
          message: 'No pongas secretos en variables VITE_*: terminan en el JavaScript que descarga cualquiera.',
        },
      ],
    },
  },
);
