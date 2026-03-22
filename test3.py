import numpy as np
import sympy as sp

def solve_simplex(c, A, b, basis):
    m, n = A.shape
    while True:
        c_b = c[basis]
        B = A[:, basis]
        B_inv = np.linalg.inv(B)
        y = c_b @ B_inv

        x_basis = B_inv @ b
        reduced_costs = c - y @ A
        if np.all(reduced_costs >= 0):
            x = np.zeros(n)
            x[basis] = x_basis
            return x

        entering = np.argmin(reduced_costs)

        d = B_inv @ A[:, entering]
        if np.all(d <= 0):
            raise ValueError("Задача неограничена")

        ratios = np.array([x_basis[i] / d[i] if d[i] > 0 else float('inf') for i in range(m)])
        leaving = np.argmin(ratios)
        
        basis[leaving] = entering

def two_phase_simplex(c, A, b):
    m, n = A.shape

    A_aux = np.hstack([A, np.eye(m)])
        
    c_aux = np.array([0] * n + [1] * m)
    basis = list(range(n, n + m))
    
    x_aux = solve_simplex(c_aux, A_aux, b, basis)
    if any(x_aux[n:] < 0):
        raise ValueError("Задача несовместна")

    basis = [i for i in basis if i < n]
    while len(basis) < m:
        basis.append(next(i for i in range(n) if i not in basis))

    return solve_simplex(c, A, b, basis)


c = np.array([-3, -4, 0, 0])  
A = np.array([[4, 1, 1, 0], [-1, 1, 0, 1]])  
b = np.array([8, 3]) 

solution = two_phase_simplex(c, A, b)
print("Оптимальное решение:", solution)
