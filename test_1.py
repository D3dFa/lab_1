import numpy as np
import sympy as sp
import sys

def process_input(obj_func, constraints, rhs):
    c = np.array([sp.Rational(float(x)) for x in obj_func], dtype=sp.Rational)

    A = np.array([[sp.Rational(float(x)) for x in row] for row in constraints], dtype=sp.Rational)

    b = np.array([sp.Rational(float(x)) for x in rhs], dtype=sp.Rational)

    return c, A, b

def build_dual_problem(obj_func, constraints, signs, rhs, var_bounds, names):
    num_constraints = len(constraints)

    dual_obj_func = rhs[:] 


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
    
    
    dual_names = [f"x_{i+1}" for i in range(num_constraints)]
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
            print(f"{names[i]} - {names[-1]}, \\quad {names[i]} \\geq 0, \\quad  {names[-1]} \\geq 0$")
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
            print(f"\\item Добавлена переменная ${names[-1]},  \\quad {names[-1]} \\geq 0$")
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

    for i in range(len(rhs)):
        if rhs[i] < 0:
            rhs[i] = -rhs[i]
            constraints[i] = [-j for j in constraints[i]]


def two_phase_simplex(c, A, b, names):
    print("\\begin{itemize}")
    m, n = A.shape
    print(f"\\subsection*{{Поиск начального базиса методом введения искуственных переменных.}}")
    
    # Шаг 1: Создание вспомогательной матрицы
    A_aux = np.hstack([A, np.eye(m, dtype=sp.Rational)])
    print(f"\\item Создана вспомогательная матрица $A_{{aux}}$:")
    print("$$" + sp.latex(sp.Matrix(A_aux)) + "$$")

    # Шаг 2: Определение вспомогательного коэффициента
    c_aux = np.array([0] * n + [1] * m, dtype=sp.Rational)
    print(f"\\item Определен вспомогательный вектор коэффициентов $c_{{aux}}$:")
    latex_elements = [sp.latex(element) for element in c_aux]
    latex_output = "$$[" + ", ".join(latex_elements) + "]$$"
    print(latex_output)

    # Шаг 3: Определение базиса
    basis = list(range(n, n + m))
    names_2 = [f'\\lambda_{i+1}' for i in range(m)]
    print(f"\\item Определен базис:")
    for i in names_2:
        print(f"${i}$")

    # Шаг 4: Решение вспомогательной задачи
    print(f"\\item Решение вспомогательной задачи с помощью симплекс-метода...")
    x_aux = solve_simplex(c_aux, A_aux, b, names + names_2, basis)
    print(f"\\item Результат решения вспомогательной задачи $x_{{aux}}$:")
    latex_elements = [sp.latex(element) for element in x_aux]
    latex_output = "$[" + ", ".join(latex_elements) + "]$"
    print(latex_output)

    # Шаг 5: Проверка на несовместность
    if any(x_aux[n:] < 0):
        raise ValueError("Задача несовместна")
    
    # Шаг 6: Обновление базиса
    basis = [i for i in basis if i < n]
    while len(basis) < m:
        for i in range(n):
            if i not in basis:
                basis.append(i)
                A[:, basis]
                try:
                    sp.Matrix(A[:, basis]).inv()
                    break
                except:
                    basis.pop()
    print(f"\\item Обновленный базис:")
    for i in basis:
        print(f"${names[i]}$")

    # Шаг 7: Решение основной задачи
    print("\\newpage")
    print(f"\\item Решение основной задачи с помощью симплекс-метода...")
    result = solve_simplex(c, A, b, names, basis)
    print("\\end{itemize}")
    return result


def solve_simplex(c, A, b, names, basis):
    m, n = A.shape
    iteration = 0
    while True:
        if iteration != 0:
            print("\\newpage")
        iteration += 1
        print(f"\\subsection*{{Итерация {iteration}}}")
        print("\\begin{itemize}")

        print("\\item \\textbf{Базисные переменные:}")
        for i in basis:
            print(f"${names[i]}$")

        B = A[:, basis]
        B_inv = np.array(sp.Matrix(B).inv(), dtype=sp.Rational)

        print("\\item \\textbf{Матрица базиса $B$:}")
        print("$" + sp.latex(sp.Matrix(B)) + "$")
        print("\\item \\textbf{Обратная матрица $B^{-1}$:}")
        print("$" + sp.latex(sp.Matrix(B_inv)) + "$")

        x_basis = B_inv @ b
        print("\\item \\textbf{Значения базисных переменных $ x_i = B^{-1} \\cdot b$:}")
        print("\\begin{enumerate}")
        for i, var in enumerate(basis):
            print(f"\\item ${names[var]} = {sp.latex(x_basis[i])}$")
        print("\\end{enumerate}")

        c_b = c[basis]
        print("\\item \\textbf{Вектор $c_b$:}")
        latex_elements = [sp.latex(element) for element in c_b]
        latex_output = "$[" + ", ".join(latex_elements) + "]$"
        print(latex_output)

        y = c_b @ B_inv
        print("\\item \\textbf{Вектор $y = c_B \\cdot B^{-1}$:}")
        latex_elements = [sp.latex(element) for element in y]
        latex_output = "$[" + ", ".join(latex_elements) + "]$"
        print(latex_output)

        reduced_costs = c - y @ A
        print("\\item \\textbf{Оценки $\\Delta = c - y \\cdot A$:}")
        latex_elements = [sp.latex(element) for element in reduced_costs]
        latex_output = "$[" + ", ".join(latex_elements) + "]$"
        print(latex_output)

        if np.all(reduced_costs >= 0):
            print("\\item \\textbf{Оптимальное решение найдено.}")
            x = np.zeros(n, dtype=sp.Rational)
            x[basis] = x_basis
            print("\\item \\textbf{Решение $x$:}")
            latex_elements = [sp.latex(element) for element in x]
            latex_output = "$[" + ", ".join(latex_elements) + "]$"
            print(latex_output)
            print("\\end{itemize}")
            return x


        entering = np.argmin(reduced_costs)
        print(f"\\item Минимальное значение у  $\\Delta_{{{entering+1}}} = { sp.latex( reduced_costs[entering] )}$, который соотвествует переменной ${names[entering]}$")

        d = B_inv @ A[:, entering]
        print("\\item \\textbf{Направление $d = B^{-1} \\cdot A_j$:}")
        latex_elements = [sp.latex(element) for element in d]
        latex_output = "$[" + ", ".join(latex_elements) + "]$"
        print(latex_output)

        if np.all(d <= 0):
            print("\\item \\textbf{Задача неограничена.}")
            print("\\end{itemize}")
            raise ValueError("Задача неограничена")


        ratios = np.array([x_basis[i] / d[i] if d[i] > 0 else sp.oo for i in range(m)], dtype=sp.Rational)
        print("\\item \\textbf{Отношения $\\theta = \\frac{x_i}{d_i}$:}")
        for i in range(m):
            if d[i] > 0:
                ratio_latex = f"\\frac{{{sp.latex(x_basis[i])}}}{{{sp.latex(d[i])}}} = {sp.latex(ratios[i])}"
            else:
                ratio_latex = "\\infty"
            print(f"\\item $\\theta_{{{i + 1}}} = {ratio_latex}$")

        leaving = np.argmin(ratios)
        print(f"\\item Минимальное значение у  $\\theta_{{{leaving+1}}} = {sp.latex(ratios[leaving])}$, который соотвествует переменной ${names[basis[leaving]]}$")

        print(f"\\item \\textbf{{Обновление базиса: ${names[basis[leaving]]} \\rightarrow {names[entering]}$}}")
        basis[leaving] = entering
        print("\\end{itemize}")


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



# obj_func = [1, -1, 1, -1, 1]
# constraints = [[6, -5, 4, -3, 2],
#                 [6, 5, 4, 3, 2], 
#                 [1, 1, 1, -1, -1], 
#                 [1, 1, 1, 1, 1]]
                
# signs = ['\\leq', '\\geq', '=', '=']
# rhs = [1, 1, 1, 1]
# var_bounds =['\\geq 0', '\\geq 0', '\\geq 0', 'free', 'free']
# names = [f'x_{i+1}' for i in range(len(obj_func))]


# obj_func = [-400, -400, -400, -400, -400, -400, 800]
# constraints = [[1, 1, 1, 1, 1, 1, 0], 
#                [-6, -4, -3, -2, -1, 0, 3], 
#                [0, -1, -2, -3, -4, -5, 2]]
# signs = ['\\leq', '\\leq', '\\leq'] 
# rhs = [20, 0, 0]
# var_bounds = ['\\geq 0' for i in range(len(obj_func))]

obj_func = [-1, -1, -1, -1, 2]
constraints = [[1, 1, 1, 1, 1], 
               [1, -1, -1, 1, 1], 
               [6, 2, -1, 1, -3], 
               [1, -3, -5, -4, 2]]
signs = ['=', '=', '\\geq', '\\leq'] 
rhs = [1, 1, -1, 1]

var_bounds = ['\\geq 0', '\\geq 0', '\\geq 0', 'free', 'free']

# obj_func = [3, 4]
# constraints = [[4, 1], [1, -1]]
# signs = ['\\leq', '\\geq']
# rhs = [8, -3]
# var_bounds =['\\geq 0', '\\geq 0']

names = [f'x_{{{i+1}}}' for i in range(len(obj_func))]


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
    obj_func = [-i for i in obj_func]

    objective_function = " + ".join([f"{coef}{name}" 
                                     for coef, name in zip(obj_func, names[:len(obj_func)]) if coef != 0])


    objective_function = " + ".join([f"{coef}{name}" for coef, name in zip(obj_func, names[:len(obj_func)]) if coef != 0])
    latex_output = f"\\begin{{equation}}\n\\begin{{matrix}}\n    F(x) = {objective_function} \\to \\min \\\\\n    \\begin{{cases}}\n    \\begin{{aligned}}\n"

    for i, (constraint, r) in enumerate(zip(constraints, rhs)):
        constraint_expr = " + ".join([f"{coef}{names[j]}" for j, coef in enumerate(constraint) if coef != 0])
        latex_output += f"        & {constraint_expr} & = {r} \\\\\n"

    bounds = []
    for name in names:
        bounds.append(f"{name} \\geq 0")

    latex_output += "        & " + ", \\quad ".join(bounds) + "\n"
    latex_output += "    \\end{aligned}\n    \\end{cases}\n\\end{matrix}\n\\end{equation}"
    print(latex_output)

    # print("\\input{simplex.tex}")

    c, A, b = process_input(obj_func, constraints, rhs)

    solution = two_phase_simplex(c, A, b, names)
    # print("\\newpage")

    print("\\section*{Выводы и результаты}")
    print("\\begin{itemize}")

    print("\\item \\textbf{Оптимальные значения переменных:}")
    sol = {}
    for i, val in enumerate(solution):
        if "\\overline{\\overline{" in names[i]:
            name = names[i].replace('\\overline{', '').replace('}', '') + "}"
            if name in sol:
                sol[name] -= val
            else:
                sol[name] = -val
        elif "\\overline{" in names[i]:
            name = names[i].replace('\\overline{', '').replace('}', '') + "}"
            if name in sol:
                sol[name] += val
            else:
                sol[name] = val
        elif "s_" in names[i]:
            pass
        else:
            sol[names[i]] = val
    for name, val in sol.items():
        print(f"\\item ${name} = {sp.latex(val)}$")
    z_value = np.dot(c[:len(solution)], solution)
    print(f"\\item \\textbf{{Значение целевой функции:}} $z = {sp.latex(-z_value)}$")
    print("\\end{itemize}")

    print('\n\n\n')
    obj_func = [-i for i in obj_func]

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

    convert_to_canonical(obj_func, constraints, signs, rhs, var_bounds, names)
    print(f"\\subsection*{{Канонический вид}}")
    # obj_func = [-i for i in obj_func]

    objective_function = " + ".join([f"{coef}{name}" for coef, name in zip(obj_func, names[:len(obj_func)]) if coef != 0])
    latex_output = f"\\begin{{equation}}\n\\begin{{matrix}}\n    F(x) = {objective_function} \\to \\min \\\\\n    \\begin{{cases}}\n    \\begin{{aligned}}\n"

    for i, (constraint, r) in enumerate(zip(constraints, rhs)):
        constraint_expr = " + ".join([f"{coef}{names[j]}" for j, coef in enumerate(constraint) if coef != 0])
        latex_output += f"        & {constraint_expr} & = {r} \\\\\n"

    bounds = []
    for name in names:
        bounds.append(f"{name} \\geq 0")

    latex_output += "        & " + ", \\quad ".join(bounds) + "\n"
    latex_output += "    \\end{aligned}\n    \\end{cases}\n\\end{matrix}\n\\end{equation}"
    print(latex_output)


    print('\n\n\n')
    c, A, b = process_input(obj_func, constraints, rhs)

    solution = two_phase_simplex(c, A, b, names)
    
    # print("\\newpage")

    print("\\section*{Выводы и результаты}")
    print("\\begin{itemize}")

    print("\\item \\textbf{Оптимальные значения переменных:}")
    sol = {}
    for i, val in enumerate(solution):
        if "\\overline{\\overline{" in names[i]:
            name = names[i].replace('\\overline{', '').replace('}', '')
            if name in sol:
                sol[name] -= val
            else:
                sol[name] = -val
        elif "\\overline{" in names[i]:
            name = names[i].replace('\\overline{', '').replace('}', '')
            if name in sol:
                sol[name] += val
            else:
                sol[name] = val
        elif "s_" in names[i]:
            pass
        else:
            sol[names[i]] = val
    for name, val in sol.items():
        print(f"\\item ${name} = {sp.latex(val)}$")

    z_value = np.dot(c[:len(solution)], solution)
    print(f"\\item \\textbf{{Значение целевой функции:}} $z = {sp.latex(z_value)}$")
    print("\\end{itemize}")


    

with open('report/output.tex', 'r', encoding='utf-8') as file:
    content = file.read()
content = content.replace('+ -', '-')
with open('report/output.tex', 'w', encoding='utf-8') as file:
    file.write(content)

