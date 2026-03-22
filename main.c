#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>
#include <time.h>

#define MAX_ROWS 100
#define MAX_COLS 100
#define TOL 1e-6

void add_error_to_b(double b[], const int size, const double error_range) {
    for (int i = 0; i < size; i++) {
        double noise = ((double)rand() / (double)RAND_MAX) * (2.0 * error_range) - error_range;
        b[i] += noise;
    }
}

void convert_to_canonical(double A[MAX_ROWS][MAX_COLS], double c[MAX_COLS],
                          const char *signs[MAX_ROWS], double b[MAX_ROWS],
                          const char *var_bounds[MAX_COLS], int *m, int *n) {
    int orig_n = *n;
    for (int i = 0; i < orig_n; i++) {
        if (strcmp(var_bounds[i], "free") == 0) {
            for (int j = 0; j < *m; j++) {
                A[j][*n] = -A[j][i];
            }
            c[*n] = -c[i];
            var_bounds[*n] = ">=";
            (*n)++;
        }
    }
    int orig_m = *m;
    for (int i = 0; i < orig_m; i++) {
        if (strcmp(signs[i], "<=") == 0) {
            for (int j = 0; j < *m; j++) {
                A[j][*n] = 0.0;
            }
            A[i][*n] = 1.0;
            c[*n] = 0.0;
            (*n)++;
        } else if (strcmp(signs[i], ">=") == 0) {
            for (int j = 0; j < *m; j++) {
                A[j][*n] = 0.0;
            }
            for (int col = 0; col < *n; col++) {
                A[i][col] = -A[i][col];
            }
            A[i][*n] = 1.0;
            b[i] = -b[i];
            c[*n] = 0.0;
            (*n)++;
        }
    }
}

void simplex(const int m, const int n, const double A[MAX_ROWS][MAX_COLS], const double b[MAX_ROWS],
            const double c[MAX_COLS], double solution[MAX_COLS], double *opt_val) {
    int i, j;
    int total_cols = n + 1;
    double tableau[MAX_ROWS][MAX_COLS + 1];
    double cost_row[MAX_COLS + 1];
    for (i = 0; i < m; i++) {
        for (j = 0; j < n; j++) {
            tableau[i][j] = A[i][j];
        }
        tableau[i][n] = b[i];
    }
    for (j = 0; j < n; j++) {
        cost_row[j] = -c[j];
    }
    cost_row[n] = 0.0;
    while (1) {
        int enter = -1;
        double min_cost = -TOL;
        for (j = 0; j < n; j++) {
            if (cost_row[j] < min_cost) {
                min_cost = cost_row[j];
                enter = j;
            }
        }
        if (enter == -1) break;
        int leave = -1;
        double min_ratio = 1e20;
        for (i = 0; i < m; i++) {
            if (tableau[i][enter] > TOL) {
                double ratio = tableau[i][n] / tableau[i][enter];
                if (ratio < min_ratio) {
                    min_ratio = ratio;
                    leave = i;
                }
            }
        }
        if (leave == -1) {
            printf("Линейная задача неограничена!\n");
            exit(1);
        }
        double pivot = tableau[leave][enter];
        for (i = 0; i < m; i++) {
            if (i != leave) {
                double factor = tableau[i][enter];
                for (j = 0; j < total_cols; j++) {
                    tableau[i][j] -= factor * tableau[leave][j] / pivot;
                }
            }
        }
        double factor = cost_row[enter] / pivot;
        for (j = 0; j < total_cols; j++) {
            cost_row[j] -= factor * tableau[leave][j];
        }
    }
    for (j = 0; j < n; j++) {
        int count_one = 0;
        int idx_one = -1;
        for (i = 0; i < m; i++) {
            if (fabs(tableau[i][j]) > TOL) {
                count_one++;
                idx_one = i;
            }
        }
        if (count_one == 1  && (tableau[idx_one][n] / tableau[idx_one][j] > 0)) {
            solution[j] = tableau[idx_one][n]/tableau[idx_one][j];
        } else {
            solution[j] = 0.0;
        }
    }
    *opt_val = cost_row[n];
}

int main(void) {
    srand((unsigned int)time(NULL));

    int m = 4;
    int n = 5;
    double A[MAX_ROWS][MAX_COLS] = {0};
    double c[MAX_COLS] = {0};
    double b[MAX_ROWS] = {0};
    A[0][0] = 6;  A[0][1] = -5; A[0][2] = 4;  A[0][3] = -3; A[0][4] = 2;
    A[1][0] = 6;  A[1][1] = 5;  A[1][2] = 4;  A[1][3] = 3;  A[1][4] = 2;
    A[2][0] = 1;  A[2][1] = 1;  A[2][2] = 1;  A[2][3] = -1; A[2][4] = -1;
    A[3][0] = 1;  A[3][1] = 1;  A[3][2] = 1;  A[3][3] = 1;  A[3][4] = 1;
    
    c[0] = 1;  c[1] = -1; c[2] = 1;  c[3] = -1; c[4] = 1;

    b[0] = 1;  b[1] = 1;  b[2] = 1;  b[3] = 1;

    // double error_range = 0.1;
    // add_error_to_b(b, m, error_range);

    const char *signs[MAX_ROWS];
    signs[0] = "<=";
    signs[1] = ">=";
    signs[2] = "=";
    signs[3] = "=";

    const char *var_bounds[MAX_COLS];
    var_bounds[0] = ">=";
    var_bounds[1] = ">=";
    var_bounds[2] = ">=";
    var_bounds[3] = "free";
    var_bounds[4] = "free";

    convert_to_canonical(A, c, signs, b, var_bounds, &m, &n);
    printf("После приведения к канонической форме: %d переменных, %d ограничений\n", n, m);
    double solution[MAX_COLS] = {0};
    double opt_val = 0.0;
    simplex(m, n, A, b, c, solution, &opt_val);
    printf("Оптимальное решение:\n");
    for (int j = 0; j < n; j++) {
        printf("x[%d] = %f\n", j, solution[j]);
    }
    printf("Оптимальное значение целевой функции: %f\n", opt_val);
    return 0;
}
