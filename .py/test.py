import numpy as np
import sympy as sp
import sys


class RedirectPrint:
    def __init__(self, filename: str):
        self.filename = filename
        self.original_stdout = sys.stdout

    def __enter__(self):
        self.file = open(self.filename, 'w')
        sys.stdout = self.file
        return self

    def __exit__(self, exc_type, exc_value, traceback):
        sys.stdout = self.original_stdout
        self.file.close()
        # with open(self.filename, 'r') as f:
        #     print(f.read())

def process_input(obj_func, constraints, rhs):
    c = []
    for x in obj_func:
        if isinstance(x, (int, float)):  
            c.append(sp.Rational(float(x)).limit_denominator())
        elif isinstance(x, sp.Basic): 
            c.append(x) 
        else:
            raise ValueError()

    A = []
    for row in constraints:
        A_row = []
        for x in row:
            if isinstance(x, (int, float)):
                A_row.append(sp.Rational(float(x)).limit_denominator())
            elif isinstance(x, sp.Basic):
                A_row.append(x)
            else:
                raise ValueError()
        A.append(A_row)

    b = []
    for x in rhs:
        if isinstance(x, (int, float)):
            b.append(sp.Rational(float(x)).limit_denominator())
        elif isinstance(x, sp.Basic):
            b.append(x)
        else:
            raise ValueError()

    return c, A, b

def build_dual_problem(obj_func, constraints, signs, rhs, var_bounds, names):
    num_constraints = len(constraints)

    # Двойственная целевая функция
    # M = sp.var('M')
    dual_obj_func = rhs[:] 

    # dual_obj_func.append(M)
    # Двойственные ограничения - транспонированная матрица коэффициентов
    dual_constraints = list(map(list, zip(*constraints)))
    dual_rhs = obj_func[:] 
    
    filtered_constraints = []
    filtered_rhs = []
    dual_var_bounds = []
    dual_var_bounds = ["free" for _ in range(num_constraints)]
    for index, constraint in enumerate(dual_constraints):
        if sum([j != 0 for j in constraint]) > 1 and dual_rhs[index] != 0:
            filtered_constraints.append(constraint)
            filtered_rhs.append(dual_rhs[index])  
        else:
            index = next((i for i, value in enumerate(constraint) if value != 0), None)
            dual_var_bounds[index] = "\\geq 0"

    dual_constraints = filtered_constraints
    dual_rhs = filtered_rhs

    dual_signs = ["\\geq" for _ in range(len(dual_constraints))]
    
    
    dual_names = [f"y_{i+1}" for i in range(num_constraints)]
    return dual_obj_func, dual_constraints, dual_signs, dual_rhs, dual_var_bounds, dual_names

def convert_to_canonical(obj_func, constraints, signs, rhs, var_bounds, names):
    n = 0
    i = 0
    di = len(var_bounds)
    print("\\begin{enumerate}")
    while i < di:
        bound = var_bounds[i]
        if bound == 'free':
            row_index = 0
            while row_index < len(constraints):
                constraints[row_index].append(-constraints[row_index][i])
                row_index += 1
            print(f"\\item Переменная ${names[i]}$ заменена на: ${names[i]} = ", end = "")
            names[i] = f"\\overline{{{names[i]}}}"
            names.append( f"\\overline{{{names[i]}}}")
            print(f"{names[i]} - {names[-1]}, {names[i]} \\geq 0,  {names[-1]} \\geq 0$")
            obj_func.append(-obj_func[i])
        i += 1

    for i in range(len(constraints)):
        sign = signs[i]
        
        if sign == '\\leq': 
            for row in constraints:
                row.append(0)
            constraints[i][-1] = 1
            obj_func.append(0)
            n += 1
            names.append(f"s_{n}")
            print(f"\\item Добавлена переменная ${names[-1]},  {names[-1]} \\geq 0$")
        elif sign == '\\geq':  
            for row in constraints:
                row.append(0)
            constraints[i] = [-i for i in constraints[i]]
            constraints[i][-1] = 1
            rhs[i] = -rhs[i]
            obj_func.append(0)
            n += 1
            names.append(f"s_{n}")
            print(f"\\item Добавлена переменная ${names[-1]},  {names[-1]} \\geq 0$")
    print("\\end{enumerate}")


def print_iteration_latex(tableau, cost_row, names, basis):
    m_table, n_total = len(tableau), len(tableau[0])          
    # header = []
    # C = cost_row[:-1] 
    # header.append("C")

    # for coef in C:
    #     header.append(f"{coef:.4g}")
    header_names = ["Базис"]  
    for i in range(len(names)):
        header_names.append(f"${names[i]}$")
    header_names.append("b")
    # header_names.append(r"$Q$")
    
    total_columns = len(header_names)
    colspec = "|c"
    for _ in range(total_columns-2):
        colspec += "X"
    colspec += "|c|"
    
    latex_lines = []
    latex_lines.append("\\begin{table}[H]")
    latex_lines.append("\\centering")
    latex_lines.append(f"\\begin{{tabularx}}{{\\textwidth}}{{{colspec}}}")
    latex_lines.append("\\hline")
    
    latex_lines.append(" & ".join(header_names) + " \\\\ \\hline")
    for i in range(m_table):
        if basis[i]:
            base_var = ", ".join([f"${names[basis[i][j]]}$" for j in range(len(basis[i])) if tableau[i][-1] / tableau[i][basis[i][j]] >= 0])
            # base_var = f"${names[basis[i][0]]}$"tableau[i][-1] / tableau[i][basis[i][j]]
        else:
            base_var = ""
        row = [base_var]
        for j in range(n_total - 1):
            value = tableau[i][j]
            # if value > 1000:
            #     formatted_value = f"{int(value):.2e}" 
            # else:
            #     formatted_value = value
            
            row.append(f"${sp.latex(value)}$")

        value = tableau[i][-1]
        # if value > 1000:
        #     formatted_value = f"{int(value):.2e}" 
        # else:
        #     formatted_value = value
        
        row.append(f"${sp.latex(value)}$")
        # t = f"${sp.latex(ratios[i])}$" if ratios[i] != np.inf else '-'
        # row.append(t)
        latex_lines.append(" & ".join(row) + " \\\\")

    latex_lines.append("\\hline")
    row_C = ["C"]
    # row_C = []
    row_C.extend(['$' + sp.latex(i) + '$' for i in cost_row])  
    # row_C.append("")
    latex_lines.append(" & ".join(row_C) + " \\\\ \\hline")
    
    
    latex_lines.append("\\end{tabularx}")
    latex_lines.append("\\end{table}")
    
    print("\n".join(latex_lines), "\n")

def simplex(c, A, b, names, tol=1e-9):
    def get_basis():
        lst = [sum([tableau[j][i] != 0  for j in range(len(tableau))]) == 1 for i in range(len(tableau[0]) - 1)]
        return [[i for i, value in enumerate(lst) if value and tableau[j][i] != 0] for j in range(len(tableau))]
    m = len(A)
    tableau = [row + [b[i]] for i, row in enumerate(A)]
    cost_row = [-value for value in c] + [0]

    iteration = 1
    print_iteration_latex(tableau, cost_row, names, get_basis())

    while True:        
        reduced_costs = cost_row[:-1]  
        # print("Текущие относительные оценки:", ", ".join(f"${sp.latex(r)}$" for r in reduced_costs))
        if all([(i >= -tol) for i in reduced_costs]):
            print("\\subsection*{Оптимальное решение найдено}")
            break

        print(f"\\subsection*{{Итерация {iteration}}}")
        print("\\begin{enumerate}")
        basis = get_basis()
        # if True:# all(basis):
        entering = np.argmin([i if isinstance(i, sp.Rational) else sp.oo  for i in reduced_costs]) 
        print(f"\\item Разрешающий столбец: {entering + 1} (значение: ${sp.latex(reduced_costs[entering])})$")

        col = [row[entering] for row in tableau]
        valid = [value > tol for value in col]

        if not any(valid):
            raise ValueError("Задача неограничена!")


        ratios = [float('inf')] * m  
        for i in range(m):
            if valid[i]:
                ratios[i] = tableau[i][-1] / col[i] 

        ratios_latex = ['$' + sp.latex(i) + '$' if i != sp.oo else '$\\infty$' for i in ratios]
        print(f"\\item Коэффициенты для нахождения разрешающей строки: {', '.join(ratios_latex)}")
        leaving = np.argmin(ratios) 
        print(f"\\item Разрешающий строка: {leaving + 1} (значение: ${sp.latex(tableau[leaving])})$")
        # else:
        #     set_basis = set(item for sublist in basis for item in sublist)
        #     for index, sublist in enumerate(basis):
        #         if not sublist:  
        #             leaving = index
        #             break 
        #     print(f"\\item Разрешающий строка: {leaving + 1} (значение: ${sp.latex(tableau[leaving])})$")
        #     for i in range(len(names)):
        #         if i not in set_basis:
        #             entering = i
        #             break
        #     print(f"\\item Разрешающий столбец: {entering + 1} (значение: ${sp.latex(reduced_costs[entering])})$")
        pivot = tableau[leaving][entering]
        print(f"\\item Значение опорного элемента: ${sp.latex(pivot)}$")
        # for j in range(len(tableau[leaving])):
        #     tableau[leaving][j] /= tableau[leaving][j]
        for i in range(m):
            if i != leaving:
                row_leaving = tableau[leaving]
                print(f"\\item Расчет $\\Delta_ {i + 1}$: {', '.join(['$' + sp.latex(tableau[i][entering] * row_leaving[j]/pivot) + '$' for j in range(len(tableau[i]))])}")
                tableau[i] = [tableau[i][j] - tableau[i][entering] * row_leaving[j]/pivot for j in range(len(tableau[i]))]
                print(f"\\item Обновление строки: {i + 1} (новое значение:{', '.join(['$' + sp.latex(j) + '$' for j in tableau[i]])})")

        row_leaving = tableau[leaving]

        factor = cost_row[entering] / pivot
        cost_row = [cost_row[j] - factor * row_leaving[j] for j in range(len(cost_row))]
        print(f"\\item Расчет $\\Delta_C$: {', '.join(['$' + sp.latex(factor * row_leaving[j]) + '$' for j in range(len(cost_row))])}")
        print(f"\\item Обновление оценок (значение: {', '.join(['$' + sp.latex(j) + '$' for j in cost_row])})")

        print("\\item Таблица после итерации:")

        print("\\end{enumerate}")
        print_iteration_latex(tableau, cost_row, names, get_basis())
        
        iteration += 1


    basis = get_basis()
    optimal_value = cost_row[-1]
    num_vars = len(c)

    x = [0] * num_vars

    for i in range(m):
        for b in basis[i]:
            if tableau[i][-1] / tableau[i][b] > 0:
                x[b] = tableau[i][-1] / tableau[i][b]
        # b = basis[i][0]   
        # x[b] = tableau[i][-1] / tableau[i][b]

    solution = x[:num_vars]


    latex_opt = []
    latex_opt.append("\\begin{table}[H]")
    latex_opt.append("\\centering")
    latex_opt.append("\\begin{tabular}{|c|c|}")
    latex_opt.append("\\hline")
    latex_opt.append("\\textbf{Переменная} & \\textbf{Значение} \\\\ \\hline")
    for idx, value in enumerate(solution):
        latex_opt.append(f"${names[idx]}$ & ${sp.latex(value)}$ \\\\ \\hline")
    latex_opt.append("\\textbf{F} & $" + f"{sp.latex(optimal_value)}" + "$ \\\\ \\hline")
    latex_opt.append("\\end{tabular}")
    latex_opt.append("\\caption{Оптимальное решение}")
    latex_opt.append("\\end{table}")
    
    print("\n".join(latex_opt))
    
    return solution, optimal_value


obj_func = [1, -1, 1, -1, 1]
constraints = [[6, -5, 4, -3, 2],
                [6, 5, 4, 3, 2], 
                [1, 1, 1, -1, -1], 
                [1, 1, 1, 1, 1]]
                
signs = ['\\leq', '\\geq', '=', '=']
rhs = [1, 1, 1, 1]
var_bounds =['\\geq 0', '\\geq 0', '\\geq 0', 'free', 'free']
names = [f'x_{i+1}' for i in range(len(obj_func))]


# obj_func = [-400, -400, -400, -400, -400, -400, 800]
# constraints = [[1, 1, 1, 1, 1, 1, 0], 
#                [-6, -4, -3, -2, -1, 0, 3], 
#                [0, -1, -2, -3, -4, -5, 2]]
# signs = ['\\leq', '\\leq', '\\leq'] 
# rhs = [20, 0, 0]
# var_bounds = ['\\geq 0' for i in range(len(obj_func))]
# names = [f'x_{i+1}' for i in range(len(obj_func))]


# obj_func = [3, 4]
# constraints = [[4, 1], [1, -1]]
# signs = ['\\leq', '\\geq']
# rhs = [8, -3]
# var_bounds =['\\geq 0', '\\geq 0']
# names = [f'x_{i+1}' for i in range(len(obj_func))]


with RedirectPrint('report/output.tex'):
    print(f"\\subsection*{{Исходная задача}}")

    objective_function = " + ".join([f"{coef}{name}" for coef, name in zip(obj_func, names[:len(obj_func)]) if coef != 0])
    latex_output = f"\\begin{{equation}}\n\\begin{{matrix}}\n    F(x) = {objective_function} \\to \\max \\\\\n    \\begin{{cases}}\n    \\begin{{aligned}}\n"

    for i, (constraint, sign, r) in enumerate(zip(constraints, signs, rhs)):
        constraint_expr = " + ".join([f"{coef}{names[j]}" for j, coef in enumerate(constraint)])
        latex_output += f"        & {constraint_expr} & {sign} {r} \\\\\n"

    bounds = []
    for i, bound in enumerate(var_bounds):
        if bound != 'free':
            bounds.append(f"{names[i]} {bound}")

    latex_output += "        & " + ", \\quad ".join(bounds) + "\n"
    latex_output += "    \\end{aligned}\n    \\end{cases}\n\\end{matrix}\n\\end{equation}"
    print(latex_output)

    print('\n\n\n')

    convert_to_canonical(obj_func, constraints, signs, rhs, var_bounds, names)

    print(f"\\subsection*{{Канонический вид}}")


    objective_function = " + ".join([f"{coef}{name}" 
                                     for coef, name in zip(obj_func, names[:len(obj_func)]) if coef != 0])


    objective_function = " + ".join([f"{coef}{name}" for coef, name in zip(obj_func, names[:len(obj_func)]) if coef != 0])
    latex_output = f"\\begin{{equation}}\n\\begin{{matrix}}\n    F(x) = {objective_function} \\to \\max \\\\\n    \\begin{{cases}}\n    \\begin{{aligned}}\n"

    for i, (constraint, sign, r) in enumerate(zip(constraints, signs, rhs)):
        constraint_expr = " + ".join([f"{coef}{names[j]}" for j, coef in enumerate(constraint) if coef != 0])
        latex_output += f"        & {constraint_expr} & {sign} {r} \\\\\n"

    bounds = []
    for name in names:
        bounds.append(f"{name} \\geq 0")

    latex_output += "        & " + ", \\quad ".join(bounds) + "\n"
    latex_output += "    \\end{aligned}\n    \\end{cases}\n\\end{matrix}\n\\end{equation}"
    print(latex_output)

    print('\n\n\n')
    print("\\input{simplex.tex}")
    print('\n\n\n')
    c, A, b = process_input(obj_func, constraints, rhs)


    sol, opt_val = simplex(c, A, b, names)
    print('\n\n\n')

    dual_obj_func, dual_constraints, dual_signs, dual_rhs, dual_var_bounds, dual_names = build_dual_problem(
        obj_func, constraints, signs, rhs, var_bounds, names
    )

    obj_func, constraints, signs, rhs, var_bounds, names = dual_obj_func, dual_constraints, dual_signs, dual_rhs, dual_var_bounds, dual_names
    print("\\newpage")
    print(f"\\subsection*{{Двойственная задача}}")
    objective_function = " + ".join([f"{coef}{name}" for coef, name in zip(obj_func, names[:len(obj_func)]) if coef != 0])
    latex_output = f"\\begin{{equation}}\n\\begin{{matrix}}\n    F(x) = {objective_function} \\to \\min \\\\\n    \\begin{{cases}}\n    \\begin{{aligned}}\n"

    for i, (constraint, sign, r) in enumerate(zip(constraints, signs, rhs)):
        constraint_expr = " + ".join([f"{coef}{names[j]}" for j, coef in enumerate(constraint) if coef != 0])
        latex_output += f"        & {constraint_expr} & {sign} {r} \\\\\n"

    bounds = []
    for i, bound in enumerate(var_bounds):
        if bound != 'free':
            bounds.append(f"{names[i]} {bound}")

    latex_output += "        & " + ", \\quad ".join(bounds) + "\n"
    latex_output += "    \\end{aligned}\n    \\end{cases}\n\\end{matrix}\n\\end{equation}"
    print(latex_output)

    print('\n\n\n')

    obj_func = [-i for i in obj_func]

    convert_to_canonical(obj_func, constraints, signs, rhs, var_bounds, names)
    print(f"\\subsection*{{Канонический вид}}")


    objective_function = " + ".join([f"{coef}{name}" for coef, name in zip(obj_func, names[:len(obj_func)]) if coef != 0])
    latex_output = f"\\begin{{equation}}\n\\begin{{matrix}}\n    F(x) = {objective_function} \\to \\max \\\\\n    \\begin{{cases}}\n    \\begin{{aligned}}\n"

    for i, (constraint, sign, r) in enumerate(zip(constraints, signs, rhs)):
        constraint_expr = " + ".join([f"{coef}{names[j]}" for j, coef in enumerate(constraint) if coef != 0])
        latex_output += f"        & {constraint_expr} & {sign} {r} \\\\\n"

    bounds = []
    for name in names:
        bounds.append(f"{name} \\geq 0")

    latex_output += "        & " + ", \\quad ".join(bounds) + "\n"
    latex_output += "    \\end{aligned}\n    \\end{cases}\n\\end{matrix}\n\\end{equation}"
    print(latex_output)


    print('\n\n\n')
    c, A, b = process_input(obj_func, constraints, rhs)


    sol, opt_val = simplex(c, A, b, names)

with open('report/output.tex', 'r', encoding='utf-8') as file:
    content = file.read()
content = content.replace('+ -', '-')
with open('report/output.tex', 'w', encoding='utf-8') as file:
    file.write(content)

