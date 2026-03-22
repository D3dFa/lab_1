import numpy as np
from scipy.optimize import linprog
import matplotlib.pyplot as plt
import pandas as pd
import sympy as sp

c = np.array([-1, -1, -1, -1, 2])
c = -c
A_eq = np.array([[1, 1, 1, 1, 1], 
               [1, -1, -1, 1, 1]]) 

b_eq = np.array([1, 1])  

A_ub = np.array([[-6, -2, 1, -1, 3], 
               [1, -3, -5, -4, 2]])

b_ub = np.array([1, 1])
bounds = [(0, None), (0, None), (0, None), (None, None), (None, None)] 

initial_solution = linprog(c, A_ub=A_ub, b_ub=b_ub, A_eq=A_eq, b_eq=b_eq, bounds=bounds, method='simplex')
true_solution = initial_solution.fun  # Истинное значение целевой функции
print(initial_solution)
def add_error_to_b(b, error_range):
    return b + np.random.uniform(-error_range, error_range, size=b.shape)

def calculate_errors(solution, true_solution):
    abs_error = np.abs(solution - true_solution)
    rel_error = np.abs(abs_error / true_solution)
    return abs_error, rel_error

num_experiments = 45
error_ranges = np.logspace(-6, -1, num=6)

abs_errors_mean = []
abs_errors_std = []
rel_errors_mean = []
rel_errors_std = []

def format_scientific(value, n=1):
    parts = "{:.10e}".format(value).split("e")

    mantissa = float(parts[0])
    exponent = int(parts[1])

    if n == -1:
        return f"$10^{{{exponent}}}$"
    
    formatted_mantissa = f"{mantissa:.{n}f}"
    
    if exponent != 0:
        return f"${formatted_mantissa} \\cdot 10^{{{exponent}}}$"
    else:
        return f"${formatted_mantissa}$"

def generate_latex_table_1(error_range, solution_with_errors, rel_errors, abs_errors):
    """Генерирует LaTeX-таблицу с промежуточными значениями"""

    max_error = np.max(abs_errors)
    order_of_magnitude = int(np.floor(np.log10(max_error)))
    n = max(1, -order_of_magnitude)

    latex_str = "\\begin{table}[h]\n"
    latex_str += "   \\centering\n"
    latex_str += f"   \\caption{{Промежуточные результаты (Размер ошибки: {format_scientific(error_range, -1)})}}\n"
    latex_str += "   \\begin{tabular}{|c|c|c|}\n"
    latex_str += "       \\hline\n"
    latex_str += "       Решение & Абсолютная ошибка & Относительная ошибка \\\\\n"
    latex_str += "       \\hline\n"

    for i in range(len(solution_with_errors)):
        latex_str += f"       {format_scientific(solution_with_errors[i], n)} & "
        latex_str += f"{format_scientific(abs_errors[i], 0)} & {format_scientific(rel_errors[i], 0)} \\\\\n"
        latex_str += "       \\hline\n"

    latex_str += "   \\end{tabular}\n"
    latex_str += "\\end{table}\n"
    return latex_str

def generate_latex_table_2(error_ranges, abs_errors_mean, abs_errors_std, rel_errors_mean, rel_errors_std):
    """Генерирует LaTeX-таблицу с усреднёнными значениями ошибок"""
    latex_str = "\\begin{table}[h]\n"
    latex_str += "   \\centering\n"
    latex_str += "   \\caption{Средние значения и стандартные отклонения для различных размеров ошибок}\n"
    latex_str += "   \\begin{tabular}{|c|c|c|c|c|}\n"
    latex_str += "       \\hline\n"
    latex_str += "       Размер ошибки & $\\overline{\\Delta_{abs}}$ & $\\sigma_{abs}$ & $\\overline{\\Delta_{rel}}$ & $\\sigma_{rel}$ \\\\\n"
    latex_str += "       \\hline\n"

    for i in range(len(error_ranges)):
        latex_str += f"       {format_scientific(error_ranges[i], -1)} & "
        latex_str += f"{format_scientific(abs_errors_mean[i])} & "
        latex_str += f"{format_scientific(abs_errors_std[i])} & "
        # n = -int("{:.10e}".format(rel_errors_std[i]).split("e")[1]) + 1
        latex_str += f"{format_scientific(rel_errors_mean[i])} & "
        latex_str += f"{format_scientific(rel_errors_std[i])} \\\\\n"
        latex_str += "       \\hline\n"

    latex_str += "   \\end{tabular}\n"
    latex_str += "\\end{table}\n"
    return latex_str


abs_errors_mean = []
abs_errors_std = []
rel_errors_mean = []
rel_errors_std = []

results_df = pd.DataFrame(columns=['Диапазон ошибок', 'Средняя абсолютная ошибка', 'Стандартное отклонение абсолютной ошибки', 
                                   'Средняя относительная ошибка', 'Стандартное отклонение относительной ошибки'])

with open(f'./report/data.tex', 'w') as file:
    for error_range in error_ranges:
        abs_errors = []
        rel_errors = []
        solution_with_errors = []
        while len(abs_errors) < num_experiments:
            b_eq_error = add_error_to_b(b_eq, error_range)
            b_ub_error = add_error_to_b(b_ub, error_range)

            solution_with_error = linprog(c, A_ub=A_ub, b_ub=b_ub_error, A_eq=A_eq, b_eq=b_eq_error, bounds=bounds, method='simplex')

            if solution_with_error.success:
                abs_error, rel_error = calculate_errors(solution_with_error.fun, true_solution)
                abs_errors.append(abs_error)
                rel_errors.append(rel_error)
                solution_with_errors.append(-solution_with_error.fun)

        abs_errors_mean.append(np.mean(abs_errors))
        abs_errors_std.append(np.std(abs_errors))
        
        rel_errors_mean.append(np.mean(rel_errors))
        rel_errors_std.append(np.std(rel_errors))

        new_row = pd.DataFrame({
            'Диапазон ошибок': [error_range],
            'Средняя абсолютная ошибка': [np.mean(abs_errors)],
            'Стандартное отклонение абсолютной ошибки': [np.std(abs_errors)],
            'Средняя относительная ошибка': [np.mean(rel_errors)],
            'Стандартное отклонение относительной ошибки': [np.std(rel_errors)]
        })

        for i, solution in enumerate(solution_with_errors):
            new_row[f'Решение {i+1}'] = [solution]

        results_df = pd.concat([results_df, new_row], ignore_index=True)
        file.write(generate_latex_table_1(error_range, solution_with_errors, rel_errors, abs_errors))
        file.write("\n\n")

    file.write(generate_latex_table_2(error_ranges, abs_errors_mean, abs_errors_std, rel_errors_mean, rel_errors_std))
    file.write("\n\n")

results_df.to_excel('./results.xlsx', index=False)


       
# Визуализация результатов
plt.figure(figsize=(12, 6))

plt.subplot(1, 2, 1)
plt.errorbar(error_ranges, abs_errors_mean, yerr=abs_errors_std, fmt='o-', color='b', capsize=5, label="Среднее ± разброс")
plt.xscale('log')
plt.yscale('log')
plt.title('Абсолютные погрешности')
plt.xlabel('Ошибка в правой части ')
plt.ylabel('Абсолютная погрешность')
plt.legend()
plt.grid()

plt.subplot(1, 2, 2)
plt.errorbar(error_ranges, rel_errors_mean, yerr=rel_errors_std, fmt='o-', color='r', capsize=5, label="Среднее ± разброс")
plt.xscale('log')
plt.yscale('log')
plt.title('Относительные погрешности')
plt.xlabel('Ошибка в правой части')
plt.ylabel('Относительная погрешность')
plt.legend()
plt.grid()

plt.tight_layout()
plt.savefig(f'./report/results.png', dpi=300)
plt.show()
